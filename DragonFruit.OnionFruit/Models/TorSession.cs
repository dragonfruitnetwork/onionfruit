// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Core;
using DragonFruit.OnionFruit.Core.Config;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Models
{
    /// <summary>
    /// Combines user configuration, control port access and the underlying tor process lifetime management into a single location
    /// </summary>
    public class TorSession : IDisposable
    {
        private readonly TorProcess _process;
        private readonly IProxyManager _proxyManager;

        private bool _disposed;

        private TorSessionState _state;
        private Timer _connectionStallTimer;

        private IList<TorrcConfigEntry> _sessionConfig;

        public TorSession(string executablePath, IProxyManager proxyManager, ILoggerFactory loggerFactory)
        {
            _proxyManager = proxyManager;
            _process = new TorProcess(executablePath, loggerFactory.CreateLogger<TorProcess>());

            Process.ProcessStateChanged += ProcessStateChanged;
            Process.BootstrapProgressChanged += ProcessBootstrapProgress;
        }

        /// <summary>
        /// The underlying Tor process
        /// </summary>
        public ITorProcessInformation Process => _process;

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
        /// Event fired when the session state changes
        /// </summary>
        public event EventHandler<TorSessionState> SessionStateChanged;

        /// <summary>
        /// Launches the Tor process, writing a config file and setting up the proxy
        /// </summary>
        public async Task StartSession()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Process.ProcessState is not TorProcess.State.Stopped and not TorProcess.State.Killed)
            {
                throw new InvalidOperationException("Cannot start a process that is already running");
            }

            _sessionConfig = GenerateSessionConfig();
            _connectionStallTimer = new Timer(_ => State = TorSessionState.ConnectingStalled, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);

            await _process.StartProcessWithConfig(_sessionConfig);

            State = TorSessionState.Connecting;
            ProcessBootstrapProgress(this, 0);
        }

        /// <summary>
        /// Builds a configuration for the current session using sane defaults and the user's preferences
        /// </summary>
        private IList<TorrcConfigEntry> GenerateSessionConfig()
        {
            var basicConfig = new ClientConfig
            {
                Endpoints = [],
                ClientOnly = true,
#if DEBUG
                EnableLogScrubbing = false
#endif
            };

            // todo handle null with error
            var targetPort = PortScanner.GetClosestFreePort(9050)!.Value;

            if (Socket.OSSupportsIPv4)
            {
                basicConfig.Endpoints.Add(new IPEndPoint(IPAddress.Loopback, targetPort));
            }

            if (Socket.OSSupportsIPv6)
            {
                basicConfig.Endpoints.Add(new IPEndPoint(IPAddress.IPv6Loopback, targetPort));
            }

            // todo add onionfruit -> torrc config converters, geoip handling + control port monitoring

            return [basicConfig];
        }

        /// <summary>
        /// Handles process state changes, setting the proxy if the process is running and clearing it if it's not
        /// </summary>
        private void ProcessStateChanged(object sender, TorProcess.State e)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            switch (e)
            {
                // todo handle killed when killswitch option is enabled

                // clear proxies
                case TorProcess.State.Killed:
                case TorProcess.State.Blocked:
                case TorProcess.State.Stopped:
                {
                    _proxyManager.SetProxy();

                    State = TorSessionState.Disconnected;
                    break;
                }

                // set blocked proxy state
                case TorProcess.State.Running when _proxyManager.GetState() != ProxyAccessState.Accessible:
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

                    _proxyManager.SetProxy(proxies);

                    State = TorSessionState.Connected;
                    break;
                }
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
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (Process.ProcessState < TorProcess.State.Stopped)
            {
                // todo stop process
            }

            _disposed = true;
            Process.ProcessStateChanged -= ProcessStateChanged;
            Process.BootstrapProgressChanged -= ProcessBootstrapProgress;
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