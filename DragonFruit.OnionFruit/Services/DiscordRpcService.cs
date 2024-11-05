// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace DragonFruit.OnionFruit.Services
{
    public class DiscordRpcService(OnionFruitSettingsStore settings, TorSession session, OnionDbService geoDb, ILogger<DiscordRpcClient> logger) : IHostedService, IDisposable
    {
        private const string FlagImageFormat = "flag-{0}";
        private const string DiscordAppId = "662238768136323102";

        private static readonly HashSet<string> SupportedFlags = new(StringComparer.OrdinalIgnoreCase)
        {
            "AD", "AE", "AF", "AM", "AO", "AR", "AT", "AU", "AZ", "BA", "BB", "BD", "BE", "BF", "BG", "BI", "BJ", "BO",
            "BR", "BS", "BW", "BY", "CA", "CG", "CH", "CI", "CL", "CM", "CN", "CO", "CR",
            "CU", "CY", "CZ", "DE", "DJ", "DK", "DO", "DZ", "EC", "EE", "EG", "ER", "ES", "ET", "FI", "FR", "GA", "GB",
            "GM", "GR", "GT", "GW", "GY", "HK", "HN", "HR", "HT", "HU", "IE", "IL", "IN",
            "IQ", "IR", "IS", "IT", "JM", "JO", "JP", "KE", "KG", "KR", "KW", "KZ", "LA", "LB", "LK", "LR", "LS", "LT",
            "LU", "LV", "LY", "MA", "MC", "MG", "MK", "ML", "MM", "MN", "MR", "MU", "MW",
            "MX", "MY", "MZ", "NA", "NL", "NO", "NZ", "PA", "PE", "PH", "PK", "PL", "PR", "PT", "QA", "RO", "RS", "RU",
            "SA", "SC", "SE", "SG", "SI", "SK", "SL", "SM", "SO", "SS", "SV", "SY", "SZ",
            "TD", "TG", "TH", "TJ", "TL", "TN", "TR", "TT", "TZ", "UA", "UG", "US", "UY", "UZ", "VE", "VN", "YE", "ZA",
            "ZW"
        };

        private CompositeDisposable _observables;
        private DiscordRpcClient _rpcClient;

        private RichPresence _currentPresence;

        private RichPresence CreatePresence(TorSession.TorSessionState state, string currentExitLocation)
        {
            var status = new RichPresence
            {
                Assets = new Assets()
            };

            switch (state)
            {
                case TorSession.TorSessionState.Disconnected:
                    status.Assets.LargeImageKey = "disconnected";
                    status.Assets.LargeImageText = "Disconnected";
                    break;

                case TorSession.TorSessionState.Connected:
                    status.Timestamps = Timestamps.Now;

                    status.Assets.LargeImageKey = "connected";
                    status.Assets.LargeImageText = "Connected";

                    // lookup the country in the local cache
                    var countryInfo = geoDb.Countries.SingleOrDefault(x => x.CountryCode.Equals(currentExitLocation));
                    if (countryInfo != null)
                    {
                        status.Assets.SmallImageKey = SupportedFlags.Contains(currentExitLocation) ? string.Format(FlagImageFormat, countryInfo.CountryCode.ToLowerInvariant()) : string.Empty;
                        status.Assets.SmallImageText = countryInfo.CountryName;
                    }

                    break;

                default:
                    return null;
            }

            return status;
        }

        private void HandleRpcStateChange(bool enabled)
        {
            switch (enabled)
            {
                case true:
                {
                    if (_rpcClient?.IsDisposed == false)
                    {
                        _rpcClient.Dispose();
                    }

                    _rpcClient = new DiscordRpcClient(DiscordAppId)
                    {
                        Logger = new NullLogger()
                    };

                    _rpcClient.OnReady += (sender, args) =>
                    {
                        logger.LogInformation("Discord RPC client ready (version {v})", args.Version);
                        (sender as DiscordRpcClient)?.SetPresence(_currentPresence);
                    };

                    _rpcClient.OnError += (_, args) =>
                    {
                        logger.LogError("Discord RPC error occurred: {message} ({type})", args.Message, args.Type);
                    };

                    _rpcClient.Initialize();
                    break;
                }

                case false when _rpcClient?.IsInitialized == true:
                    _rpcClient.ClearPresence();
                    _rpcClient.Dispose();
                    break;
            }
        }

        private void UpdatePresence(RichPresence p)
        {
            _currentPresence = p;

            // push the presence if enabled
            if (_rpcClient?.IsInitialized == true)
            {
                _rpcClient.SetPresence(p);
            }
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _observables?.Dispose();
            _observables = new CompositeDisposable();

            var rpcEnabled = settings.GetObservableValue<bool>(OnionFruitSetting.EnableDiscordRpc);
            var currentExitCountry = settings.GetObservableValue<string>(OnionFruitSetting.TorExitCountryCode);
            var sessionState = Observable.FromEventPattern<EventHandler<TorSession.TorSessionState>, TorSession.TorSessionState>(h => session.SessionStateChanged += h, h => session.SessionStateChanged -= h)
                .StartWith(new EventPattern<TorSession.TorSessionState>(this, session.State));

            // setup observable subscriptions
            rpcEnabled.ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(HandleRpcStateChange)
                .DisposeWith(_observables);

            sessionState.CombineLatest(currentExitCountry, rpcEnabled)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Where(x => x.Third) // only generate the presence if the rpc is enabled
                .Select(x => CreatePresence(x.First.EventArgs, x.Second)) // generate the presence
                .Where(x => x != null) // filter out null presences
                .Subscribe(UpdatePresence) // push the presence
                .DisposeWith(_observables);

            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
            _rpcClient?.Dispose();
            _observables?.Dispose();
        }
    }
}