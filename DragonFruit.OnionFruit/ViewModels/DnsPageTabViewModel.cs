// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Services;
using DynamicData;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class DnsPageTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly SourceList<IPAddress> _fallbackDnsServersSource;

        private readonly IProcessElevator _processElevator;
        private readonly INetworkAdapterManager _adapterManager;

        private readonly ObservableAsPropertyHelper<bool> _dnsProxyEnabled, _canToggleDns, _isCustomAlternativeDnsServerSelected;
        private readonly ObservableAsPropertyHelper<FALLBACK_DNS_SERVER_PRESET> _dnsFallbackServerPreset;

        private readonly ReadOnlyObservableCollection<IPAddress> _alternativeDnsServers;

        private readonly CompositeDisposable _disposables = new();

        private string _customDnsServerEntryContent;

        public DnsPageTabViewModel(OnionFruitSettingsStore settings, INetworkAdapterManager adapterManager, IProcessElevator processElevator)
        {
            _settings = settings;
            _adapterManager = adapterManager;
            _processElevator = processElevator;

            settings.GetObservableValue<bool>(OnionFruitSetting.DnsProxyingEnabled)
                .ObserveOn(RxApp.MainThreadScheduler)
                // disable switch if not running as an admin (even though it is technically on)
                .Select(x => adapterManager.DnsState == NetworkComponentState.Available && x)
                .ToProperty(this, x => x.DnsProxyEnabled, out _dnsProxyEnabled)
                .DisposeWith(_disposables);

            settings.GetObservableValue<FALLBACK_DNS_SERVER_PRESET>(OnionFruitSetting.DnsFallbackServerPreset)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedAlternativeDnsServerPreset, out _dnsFallbackServerPreset)
                .DisposeWith(_disposables);

            _fallbackDnsServersSource = settings.GetCollection<IPAddress>(OnionFruitSetting.DnsCustomFallbackServers);
            _fallbackDnsServersSource
                .Connect()
                .Bind(out _alternativeDnsServers)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(AlternativeDnsServers));
                    this.RaisePropertyChanged(nameof(NoAlternativeServersAvailable));
                })
                .DisposeWith(_disposables);

            // allow toggling if already enabled, otherwise only allow if dns is available
            this.WhenAnyValue(x => x.DnsProxyEnabled)
                .Select(x => x || adapterManager.DnsState == NetworkComponentState.Available)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CanToggleDns, out _canToggleDns);

            this.WhenAnyValue(x => x.SelectedAlternativeDnsServerPreset)
                .Select(x => x == FALLBACK_DNS_SERVER_PRESET.Custom)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsCustomAlternativeDnsServerSelected, out _isCustomAlternativeDnsServerSelected);

            RelaunchAsElevatedProcess = ReactiveCommand.Create(() => processElevator.RelaunchProcess(true));
        }

        public IconSource ShieldIcon => App.GetIcon(LucideIconNames.ShieldHalf);
        public IconSource DnsProxyingIcon => App.GetIcon(LucideIconNames.Waypoints);
        public IconSource AlternativeServersIcon => App.GetIcon(LucideIconNames.BookDashed);

        public FALLBACK_DNS_SERVER_PRESET[] AlternativeDnsPresets { get; } = Enum.GetValues<FALLBACK_DNS_SERVER_PRESET>();

        /// <summary>
        /// Determines whether the DNS proxy can be toggled on or off.
        /// </summary>
        /// <remarks>
        /// A user can always toggle the proxy off, but is only able to turn it on if the DNS can be changed without relaunching as admin or whatever.
        /// This way, a user knows why OnionFruit requests admin permissions as they're forced to reload the app.
        /// </remarks>
        public bool CanToggleDns => _canToggleDns.Value;

        public bool ShowNotAvailableNotice => _adapterManager.DnsState == NetworkComponentState.Unavailable;
        public bool ShowMissingPermissionsNotice => _adapterManager.DnsState == NetworkComponentState.MissingPermissions && _processElevator.CheckElevationStatus() == ElevationStatus.CanElevate;

        public bool IsCustomAlternativeDnsServerSelected => _isCustomAlternativeDnsServerSelected.Value;
        public bool NoAlternativeServersAvailable => _alternativeDnsServers.Count == 0;

        public IReadOnlyCollection<IPAddress> AlternativeDnsServers => _alternativeDnsServers;

        public bool DnsProxyEnabled
        {
            get => _dnsProxyEnabled.Value;
            set => _settings.SetValue(OnionFruitSetting.DnsProxyingEnabled, value);
        }

        public FALLBACK_DNS_SERVER_PRESET SelectedAlternativeDnsServerPreset
        {
            get => _dnsFallbackServerPreset.Value;
            set => _settings.SetValue(OnionFruitSetting.DnsFallbackServerPreset, value);
        }

        public string CustomDnsServerEntryContent
        {
            get => _customDnsServerEntryContent;
            set => this.RaiseAndSetIfChanged(ref _customDnsServerEntryContent, value);
        }

        public ICommand RelaunchAsElevatedProcess { get; }

        public void AddDnsServerEntry()
        {
            var ipContent = CustomDnsServerEntryContent.Trim();

            if (IPAddress.TryParse(ipContent, out var ip) && ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            {
                if (AlternativeDnsServers.Contains(ip))
                {
                    CustomDnsServerEntryContent = string.Empty;
                    return;
                }

                _fallbackDnsServersSource.Add(ip);
                CustomDnsServerEntryContent = string.Empty;
            }
            else
            {
                CustomDnsServerEntryContent = ipContent;
            }
        }

        public void RemoveDnsServerEntry(IPAddress ip)
        {
            _fallbackDnsServersSource.Remove(ip);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}