// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Reactive.Linq;
using System.Windows.Input;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Services;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class DnsPageTabViewModel : ReactiveObject
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly INetworkAdapterManager _adapterManager;
        private readonly IProcessElevator _processElevator;

        private readonly ObservableAsPropertyHelper<bool> _dnsProxyEnabled;
        private readonly ObservableAsPropertyHelper<bool> _canToggleDns, _showRelaunchNotice;

        public DnsPageTabViewModel(OnionFruitSettingsStore settings, INetworkAdapterManager adapterManager, IProcessElevator processElevator)
        {
            _settings = settings;
            _adapterManager = adapterManager;
            _processElevator = processElevator;

            settings.GetObservableValue<bool>(OnionFruitSetting.DnsEnabled)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.DnsProxyEnabled, out _dnsProxyEnabled);

            // dns server list

            Observable.Never<ElevationStatus>()
                .StartWith(_processElevator.CheckElevationStatus())
                .Select(x => x == ElevationStatus.CanElevate)
                .ToProperty(this, x => x.ShowRelaunchNotice, out _showRelaunchNotice);

            // allow toggling if already enabled, otherwise only allow if dns is available
            this.WhenAnyValue(x => x.DnsProxyEnabled)
                .Select(x => x || _adapterManager.DnsState == NetworkComponentState.Available)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CanToggleDns, out _canToggleDns);

            RelaunchAsElevatedProcess = ReactiveCommand.Create(() => processElevator.RelaunchProcess(true));
        }

        public IconSource ShieldIcon => App.GetIcon(LucideIconNames.ShieldHalf);

        /// <summary>
        /// Determines whether the DNS proxy can be toggled on or off.
        /// </summary>
        /// <remarks>
        /// A user can always toggle the proxy off, but is only able to turn it on if the DNS can be changed without relaunching as admin or whatever.
        /// This way, a user knows why OnionFruit requests admin permissions as they're forced to reload the app.
        /// </remarks>
        public bool CanToggleDns => _canToggleDns.Value;

        public bool ShowRelaunchNotice => _showRelaunchNotice.Value;

        public bool DnsProxyEnabled
        {
            get => _dnsProxyEnabled.Value;
            set => _settings.SetValue(OnionFruitSetting.DnsEnabled, value);
        }

        public ICommand RelaunchAsElevatedProcess { get; }
    }
}