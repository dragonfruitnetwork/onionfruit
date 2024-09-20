// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Reactive.Disposables;
using System.Windows.Input;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Services;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class LandingPageSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<bool> _connectedPageEnabled, _disconnectedPageEnabled;
        private readonly ObservableAsPropertyHelper<string> _connectedPage, _disconnectedPage;

        public LandingPageSettingsTabViewModel(OnionFruitSettingsStore settings)
        {
            _settings = settings;

            _connectedPage = settings.GetObservableValue<string>(OnionFruitSetting.WebsiteLaunchConnect).ToProperty(this, x => x.ConnectedPage).DisposeWith(_disposables);
            _disconnectedPage = settings.GetObservableValue<string>(OnionFruitSetting.WebsiteLaunchDisconnect).ToProperty(this, x => x.DisconnectedPage).DisposeWith(_disposables);

            _connectedPageEnabled = settings.GetObservableValue<bool>(OnionFruitSetting.EnableWebsiteLaunchConnect).ToProperty(this, x => x.EnableConnectedPage).DisposeWith(_disposables);
            _disconnectedPageEnabled = settings.GetObservableValue<bool>(OnionFruitSetting.EnableWebsiteLaunchDisconnect).ToProperty(this, x => x.EnableDisconnectedPage).DisposeWith(_disposables);

            LaunchUrl = ReactiveCommand.Create<string>(url => App.Launch(string.IsNullOrWhiteSpace(url) ? LandingPageLaunchService.DefaultConnectionPage : url), outputScheduler: RxApp.TaskpoolScheduler);
        }

        public IconSource ConnectedPageIcon => App.GetIcon(LucideIconNames.ShieldCheck);
        public IconSource DisconnectedPageIcon => App.GetIcon(LucideIconNames.ShieldBan);

        public bool EnableConnectedPage
        {
            get => _connectedPageEnabled.Value;
            set => _settings.SetValue(OnionFruitSetting.EnableWebsiteLaunchConnect, value);
        }

        public bool EnableDisconnectedPage
        {
            get => _disconnectedPageEnabled.Value;
            set => _settings.SetValue(OnionFruitSetting.EnableWebsiteLaunchDisconnect, value);
        }

        // todo add a URI parse step, only set backing field if passing
        public string ConnectedPage
        {
            get => _connectedPage.Value;
            set => _settings.SetValue(OnionFruitSetting.WebsiteLaunchConnect, value);
        }

        public string DisconnectedPage
        {
            get => _disconnectedPage.Value;
            set => _settings.SetValue(OnionFruitSetting.WebsiteLaunchDisconnect, value);
        }

        public ICommand LaunchUrl { get; }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}