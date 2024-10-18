// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DragonFruit.OnionFruit.Configuration;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class ExternalConnectionsSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        
        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<bool> _enableDiscordRpc;
        private readonly ObservableAsPropertyHelper<bool> _enableCrashReporting;

        public ExternalConnectionsSettingsTabViewModel(OnionFruitSettingsStore settings)
        {
            _settings = settings;

            settings.GetObservableValue<bool>(OnionFruitSetting.EnableDiscordRpc)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.EnableDiscordRpc, out _enableDiscordRpc)
                .DisposeWith(_disposables);

            settings.GetObservableValue<bool>(OnionFruitSetting.EnableErrorReporting)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.EnableErrorReporting, out _enableCrashReporting)
                .DisposeWith(_disposables);
        }

        public IconSource DiscordIcon => App.GetIcon(LucideIconNames.Gamepad);
        public IconSource ErrorIcon => App.GetIcon(LucideIconNames.Bug);

        public bool EnableDiscordRpc
        {
            get => _enableDiscordRpc.Value;
            set => _settings.SetValue(OnionFruitSetting.EnableDiscordRpc, value);
        }

        public bool EnableErrorReporting
        {
            get => _enableCrashReporting.Value;
            set => _settings.SetValue(OnionFruitSetting.EnableErrorReporting, value);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}