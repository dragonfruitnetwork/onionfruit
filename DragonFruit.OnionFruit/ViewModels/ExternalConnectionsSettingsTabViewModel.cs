// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Services;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class ExternalConnectionsSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly IStartupLaunchService _startupLaunchService;

        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<bool> _enableDiscordRpc;
        private readonly ObservableAsPropertyHelper<bool> _enableCrashReporting;
        private readonly ObservableAsPropertyHelper<bool> _startupEnabled, _startupBlocked, _forceStartupRepair;

        private StartupLaunchState _currentStartupState;

        public ExternalConnectionsSettingsTabViewModel(OnionFruitSettingsStore settings)
        {
            _settings = settings;
            _startupLaunchService = App.Instance.Services.GetService<IStartupLaunchService>();
            _currentStartupState = _startupLaunchService?.CurrentStartupState ?? StartupLaunchState.Blocked;

            settings.GetObservableValue<bool>(OnionFruitSetting.EnableDiscordRpc)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.EnableDiscordRpc, out _enableDiscordRpc)
                .DisposeWith(_disposables);

            settings.GetObservableValue<bool>(OnionFruitSetting.EnableErrorReporting)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.EnableErrorReporting, out _enableCrashReporting)
                .DisposeWith(_disposables);

            this.WhenAnyValue(x => x.CurrentStartupState)
                .Select(x => x == StartupLaunchState.Blocked)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsStartupBlocked, out _startupBlocked);

            this.WhenAnyValue(x => x.CurrentStartupState)
                .Select(x => x == StartupLaunchState.Enabled)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsStartupEnabled, out _startupEnabled);

            this.WhenAnyValue(x => x.CurrentStartupState)
                .Select(x => x == StartupLaunchState.EnabledInvalid)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ForceStartupRepair, out _forceStartupRepair);

            RepairStartup = ReactiveCommand.CreateRunInBackground(() => SetStartupStateImpl(true));
            SetStartupState = ReactiveCommand.CreateRunInBackground<bool>(SetStartupStateImpl);
        }

        public IconSource DiscordIcon => App.GetIcon(LucideIconNames.Gamepad);
        public IconSource ErrorIcon => App.GetIcon(LucideIconNames.Bug);
        public IconSource StartupIcon => App.GetIcon(LucideIconNames.Power);

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

        public StartupLaunchState CurrentStartupState
        {
            get => _currentStartupState;
            private set => this.RaiseAndSetIfChanged(ref _currentStartupState, value);
        }

        public bool IsStartupEnabled => _startupEnabled.Value;
        public bool IsStartupBlocked => _startupBlocked.Value;
        public bool ForceStartupRepair => _forceStartupRepair.Value;

        public ICommand RepairStartup { get; }
        public ICommand SetStartupState { get; }

        private void SetStartupStateImpl(bool enabled)
        {
            CurrentStartupState = _startupLaunchService.SetStartupState(enabled);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}