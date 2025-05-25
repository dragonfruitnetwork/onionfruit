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

        private readonly ObservableAsPropertyHelper<bool> _dnsProxyEnabled;
        private readonly ObservableAsPropertyHelper<bool> _canToggleDns, _showRelaunchNotice;

        private readonly ReadOnlyObservableCollection<IPAddress> _alternativeDnsServers;

        private readonly CompositeDisposable _disposables = new();

        private string _addDnsServerEntryBoxContent;

        public DnsPageTabViewModel(OnionFruitSettingsStore settings, INetworkAdapterManager adapterManager, IProcessElevator processElevator)
        {
            _settings = settings;

            settings.GetObservableValue<bool>(OnionFruitSetting.DnsEnabled)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.DnsProxyEnabled, out _dnsProxyEnabled)
                .DisposeWith(_disposables);

            _fallbackDnsServersSource = settings.GetCollection<IPAddress>(OnionFruitSetting.DnsFallbackServers);
            _fallbackDnsServersSource
                .Connect()
                .Bind(out _alternativeDnsServers)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(AlternativeDnsServers));
                    this.RaisePropertyChanged(nameof(NoAlternativeServersAvailable));
                })
                .DisposeWith(_disposables);

            Observable.Never<ElevationStatus>()
                .StartWith(processElevator.CheckElevationStatus())
                .Select(x => x == ElevationStatus.CanElevate)
                .ToProperty(this, x => x.ShowRelaunchNotice, out _showRelaunchNotice);

            // allow toggling if already enabled, otherwise only allow if dns is available
            this.WhenAnyValue(x => x.DnsProxyEnabled)
                .Select(x => x || adapterManager.DnsState == NetworkComponentState.Available)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CanToggleDns, out _canToggleDns);

            RelaunchAsElevatedProcess = ReactiveCommand.Create(() => processElevator.RelaunchProcess(true));
        }

        public IconSource ShieldIcon => App.GetIcon(LucideIconNames.ShieldHalf);
        public IconSource DnsProxyingIcon => App.GetIcon(LucideIconNames.Waypoints);
        public IconSource AlternativeServersIcon => App.GetIcon(LucideIconNames.BookDashed);

        /// <summary>
        /// Determines whether the DNS proxy can be toggled on or off.
        /// </summary>
        /// <remarks>
        /// A user can always toggle the proxy off, but is only able to turn it on if the DNS can be changed without relaunching as admin or whatever.
        /// This way, a user knows why OnionFruit requests admin permissions as they're forced to reload the app.
        /// </remarks>
        public bool CanToggleDns => _canToggleDns.Value;

        /// <summary>
        /// Controls whether the "admin relaunch required" message is shown
        /// </summary>
        public bool ShowRelaunchNotice => _showRelaunchNotice.Value;

        public bool NoAlternativeServersAvailable => _alternativeDnsServers.Count == 0;

        public IReadOnlyCollection<IPAddress> AlternativeDnsServers => _alternativeDnsServers;

        public bool DnsProxyEnabled
        {
            get => _dnsProxyEnabled.Value;
            set => _settings.SetValue(OnionFruitSetting.DnsEnabled, value);
        }

        public string AddDnsServerEntryBoxContent
        {
            get => _addDnsServerEntryBoxContent;
            set => this.RaiseAndSetIfChanged(ref _addDnsServerEntryBoxContent, value);
        }

        public ICommand RelaunchAsElevatedProcess { get; }

        public void AddDnsServerEntry()
        {
            var ipContent = AddDnsServerEntryBoxContent.Trim();

            if (IPAddress.TryParse(ipContent, out var ip) && ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            {
                if (AlternativeDnsServers.Contains(ip))
                {
                    AddDnsServerEntryBoxContent = string.Empty;
                    return;
                }

                _fallbackDnsServersSource.Add(ip);
                AddDnsServerEntryBoxContent = string.Empty;
            }
            else
            {
                AddDnsServerEntryBoxContent = ipContent;
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