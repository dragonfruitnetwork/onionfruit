// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core;
using DragonFruit.OnionFruit.Core.Config;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Core.Transports;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Services;
using DynamicData;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Models
{
    /// <summary>
    /// Combines user configuration, control port access and the underlying tor process lifetime management into a single location
    /// </summary>
    public class TorSession(ExecutableLocator executableLocator, IProxyManager proxyManager, IOnionDatabase database, TransportManager transportManager, OnionFruitSettingsStore settings, ILoggerFactory loggerFactory)
    {
        private const int DefaultSocksPort = 9050;
        private const int DefaultControlPort = 9051;

        private TorProcess _process;
        private TorSessionState _state = TorSessionState.Disconnected;

        private Timer _connectionStallTimer;
        private IReadOnlyList<TorrcConfigEntry> _sessionConfig;

        /// <summary>
        /// The current session state
        /// </summary>
        public TorSessionState State
        {
            get => _state;
            private set
            {
                if (_state == value) return;

                _state = value;
                SessionStateChanged?.Invoke(this, value);
            }
        }

        /// <summary>
        /// Event fired when the underlying tor process' bootstrap progress changes
        /// </summary>
        public event EventHandler<int> BootstrapProgressChanged;

        /// <summary>
        /// Event fired when the session state changes
        /// </summary>
        public event EventHandler<TorSessionState> SessionStateChanged;

        /// <summary>
        /// Launches the Tor process, writing a config file and setting up the proxy
        /// </summary>
        public async Task StartSession()
        {
            // cleanup process if not already done
            if (_process != null)
            {
                if (_process.ProcessState is not TorProcess.State.Stopped and not TorProcess.State.Killed)
                {
                    throw new InvalidOperationException("Cannot start a process that is already running");
                }

                _process.ProcessStateChanged -= ProcessStateChanged;
                _process.BootstrapProgressChanged -= ProcessBootstrapProgress;
            }

            // wait up to 15 seconds for geoip file generation task to complete
            IReadOnlyDictionary<AddressFamily, FileInfo> geoIpFiles = null;

            if (database.State == DatabaseState.Ready)
            {
                try
                {
                    geoIpFiles = await database.GeoIPFiles.WaitAsync(TimeSpan.FromSeconds(15));
                }
                catch (TimeoutException)
                {
                    // reset user's country preferences to "random" if GeoIP files aren't available due to timeout
                    settings.SetValue(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode);
                    settings.SetValue(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode);
                }
                catch (Exception e)
                {
                    loggerFactory.CreateLogger<IOnionDatabase>().LogWarning(e, "GeoIP write task failed unexpectedly: {message}", e.Message);
                }
            }
            else
            {
                // if the database isn't ready, set the country codes to "random"
                // this also allows the UI to be refreshed to let the user know in the event the database does finish loading
                settings.SetValue(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode);
                settings.SetValue(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode);
            }

            // create session config and underlying process
            if (!TryGenerateSessionConfig(geoIpFiles, out _sessionConfig) || !TryCreateUnderlyingTorProcess(out _process))
            {
                return;
            }

            // subscribe to process events
            _process.ProcessStateChanged += ProcessStateChanged;
            _process.BootstrapProgressChanged += ProcessBootstrapProgress;

            try
            {
                State = TorSessionState.Connecting;
                await _process.StartProcessWithConfig(_sessionConfig);
            }
            catch (Exception e)
            {
                State = TorSessionState.BlockedProcess;
                loggerFactory.CreateLogger<TorSession>().LogWarning("Failed to start Tor process: {message}", e.Message);
                return;
            }

            // start stall timer
            _connectionStallTimer = new Timer(_ => State = TorSessionState.ConnectingStalled, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            ProcessBootstrapProgress(this, 0);
        }

        /// <summary>
        /// Stops the current tor session, cleaning up timers and configuration files.
        /// </summary>
        public async ValueTask StopSession()
        {
            switch (_process.ProcessState)
            {
                case TorProcess.State.Killed:
                    // stop process was already called by the underlying process handler, demote to "disconnected"
                    State = TorSessionState.Disconnected;
                    return;

                case TorProcess.State.Stopped:
                    throw new InvalidOperationException("Cannot stop a process that is not running");
            }

            State = TorSessionState.Disconnecting;

            if (_connectionStallTimer != null)
            {
                await _connectionStallTimer.DisposeAsync();
            }

            // allow disconnecting state to propagate
            await Task.Delay(750).ConfigureAwait(false);

            // underlying process state change will cause the State to be set to Disconnected without manual intervention.
            _process.StopProcess();
        }

        /// <summary>
        /// Builds a configuration for the current session using sane defaults and the user's preferences
        /// </summary>
        private bool TryGenerateSessionConfig(IReadOnlyDictionary<AddressFamily, FileInfo> geoIpFiles, out IReadOnlyList<TorrcConfigEntry> sessionConfig)
        {
            var consumedPorts = new List<int>(2);
            var basicConfig = new ClientConfig
            {
                Endpoints = [],
                ClientOnly = true,
#if DEBUG
                EnableLogScrubbing = false
#endif
            };

            // todo handle null with error/create special method
            var targetPort = PortScanner.GetClosestFreePort(DefaultSocksPort, excludedPorts: consumedPorts)!.Value;

            if (Socket.OSSupportsIPv4)
                basicConfig.Endpoints.Add(new IPEndPoint(IPAddress.Loopback, targetPort));

            if (Socket.OSSupportsIPv6)
                basicConfig.Endpoints.Add(new IPEndPoint(IPAddress.IPv6Loopback, targetPort));

            // don't reuse SOCKS port
            consumedPorts.Add(targetPort);

            var controlPort = PortScanner.GetClosestFreePort(DefaultControlPort, excludedPorts: consumedPorts)!.Value;
            var controlPortConfig = new ControlPortConfig(basicConfig.Endpoints.Select(x => new IPEndPoint(x.Address, controlPort)), Guid.NewGuid().ToString("N"));

            consumedPorts.Add(controlPort);

            // node selection (GeoIP, countries)
            var nodeSelectionConfig = new NodeSelectionConfig();

            if (geoIpFiles != null)
            {
                if (geoIpFiles.TryGetValue(AddressFamily.InterNetwork, out var ipv4File))
                    nodeSelectionConfig.GeoIPv4File = ipv4File.FullName;

                if (geoIpFiles.TryGetValue(AddressFamily.InterNetworkV6, out var ipv6File))
                    nodeSelectionConfig.GeoIPv6File = ipv6File.FullName;

                if (database.State == DatabaseState.Ready)
                {
                    var entryCountry = settings.GetValue<string>(OnionFruitSetting.TorEntryCountryCode);
                    if (entryCountry != IOnionDatabase.TorCountryCode)
                        nodeSelectionConfig.EntryNodes = [new NodeCountryFilter(entryCountry)];

                    var exitCountry = settings.GetValue<string>(OnionFruitSetting.TorExitCountryCode);
                    if (exitCountry != IOnionDatabase.TorCountryCode)
                        nodeSelectionConfig.ExitNodes = [new NodeCountryFilter(exitCountry)];
                }
            }

            // handle firewall settings
            if (settings.GetValue<bool>(OnionFruitSetting.EnableFirewallPortRestrictions))
            {
                basicConfig.FascistFirewall = true;

                using (settings.GetCollection<uint>(OnionFruitSetting.AllowedFirewallPorts).Connect().Bind(out var ports).Subscribe())
                {
                    basicConfig.FirewallPorts = ports.Select(x => (int)x).ToList();
                }
            }

            // bridge settings
            var bridgeConfig = new CustomConfig();
            var selectedTransport = settings.GetValue<TransportType>(OnionFruitSetting.SelectedTransportType);

            if (selectedTransport != TransportType.None && transportManager.AvailableTransports.TryGetValue(selectedTransport, out var transportInfo))
            {
                List<string> configLines =
                [
                    "UseBridges 1",
                    ..transportManager.TransportConfigLines.Values
                ];

                IEnumerable<string> bridgeLines;

                using (settings.GetCollection<string>(OnionFruitSetting.UserDefinedBridges).Connect().Bind(out var lines).Subscribe())
                {
                    // clone list to avoid concurrency issues
                    bridgeLines = lines.ToList();
                }

                foreach (var line in bridgeLines.ToList())
                {
                    var lineInfo = BridgeEntry.ValidationRegex().Match(line);

                    if (!lineInfo.Success)
                    {
                        // todo remove invalid lines from config?
                        continue;
                    }

                    if (lineInfo.Groups["type"].Value != transportInfo.Id)
                    {
                        continue;
                    }

                    configLines.Add($"Bridge {line}");
                }

                // handle default bridges fallback
                if (configLines.Count > 1 && transportManager.Config.Bridges.TryGetValue(transportInfo.DefaultBridgeKey ?? string.Empty, out var defaults))
                {
                    configLines.AddRange(defaults.Select(x => $"Bridge {x}"));
                }

                bridgeConfig.Lines = configLines;

                // cannot use bridges and entry nodes at the same time
                nodeSelectionConfig.EntryNodes?.Clear();
            }

            sessionConfig = [basicConfig, controlPortConfig, nodeSelectionConfig, bridgeConfig];
            return true;
        }

        /// <summary>
        /// Handles process state changes, setting the proxy if the process is running and clearing it if it's not
        /// </summary>
        private async void ProcessStateChanged(object sender, TorProcess.State e)
        {
            switch (e)
            {
                case TorProcess.State.Started:
                case TorProcess.State.Bootstrapping:
                {
                    State = TorSessionState.Connecting;
                    break;
                }

                // set blocked proxy state
                case TorProcess.State.Running when proxyManager.GetState() != ProxyAccessState.Accessible:
                {
                    State = TorSessionState.BlockedProxy;
                    break;
                }

                // set proxy
                case TorProcess.State.Running:
                {
                    var endpoints = _sessionConfig.OfType<ClientConfig>().Single().Endpoints;
                    var proxies = new NetworkProxy[endpoints.Count];
                    var index = 0;

                    foreach (var endpoint in endpoints)
                    {
                        var address = $"socks://{endpoint}";
                        proxies[index++] = new NetworkProxy(true, new Uri(address));
                    }

                    await proxyManager.SetProxy(proxies);

                    State = TorSessionState.Connected;
                    break;
                }

                case TorProcess.State.Killed when !settings.GetValue<bool>(OnionFruitSetting.DisconnectOnTorFailure):
                {
                    State = TorSessionState.KillSwitchTriggered;
                    break;
                }

                // clear proxies
                case TorProcess.State.Killed:
                case TorProcess.State.Stopped:
                {
                    await proxyManager.SetProxy();

                    if (_connectionStallTimer != null)
                    {
                        await _connectionStallTimer.DisposeAsync();
                    }

                    State = TorSessionState.Disconnected;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }

        /// <summary>
        /// Handles bootstrap progress updates, resetting the "stall timer" if progress is made or disabling it if the connection has been established
        /// </summary>
        private void ProcessBootstrapProgress(object sender, int e)
        {
            if (e == 100)
            {
                _connectionStallTimer?.Dispose();
                _connectionStallTimer = null;
            }
            else
            {
                _connectionStallTimer?.Change(TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
            }

            // forward progress using persistent event (as process is frequently replaced)
            BootstrapProgressChanged?.Invoke(sender, e);
        }

        private bool TryCreateUnderlyingTorProcess(out TorProcess process)
        {
            var torExecutable = executableLocator.LocateExecutableInstancesOf("tor").ToList();

            if (torExecutable.Count == 0)
            {
                process = null;
                return false;
            }

            process = new TorProcess(torExecutable.First(), loggerFactory.CreateLogger<TorProcess>());
            return true;
        }

        public enum TorSessionState
        {
            /// <summary>
            /// Something has blocked the process from starting
            /// </summary>
            BlockedProcess,

            /// <summary>
            /// Something has blocked the proxy from being set
            /// </summary>
            BlockedProxy,

            /// <summary>
            /// The process was killed, and the killswitch was triggered.
            /// </summary>
            KillSwitchTriggered,

            /// <summary>
            /// The session is closing
            /// </summary>
            Disconnecting,

            /// <summary>
            /// The session is disconnected
            /// </summary>
            Disconnected,

            /// <summary>
            /// The session is connecting
            /// </summary>
            Connecting,

            /// <summary>
            /// The session is connecting, but progress hasn't advanced in some time...
            /// </summary>
            ConnectingStalled,

            /// <summary>
            /// The session has connected successfully
            /// </summary>
            Connected
        }
    }
}