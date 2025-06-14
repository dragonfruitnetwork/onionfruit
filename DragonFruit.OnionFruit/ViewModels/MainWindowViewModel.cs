// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.Updater;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    /// <summary>
    /// Represents the content displayed in the main window toolbar/ribbon
    /// </summary>
    /// <param name="ToggleChecked">Whether the connection toggle is switched</param>
    /// <param name="AllowToggling">Whether the toggle can be clicked</param>
    /// <param name="Background">The background colour to use</param>
    /// <param name="Text">The text to display on the left of the toolbar</param>
    public record ToolbarContent(bool ToggleChecked, bool AllowToggling, IImmutableSolidColorBrush Background, string Text);

    public class MainWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly CompositeDisposable _disposables = new();

        private readonly TorSession _session;
        private readonly OnionFruitSettingsStore _settings;
        private readonly IServiceProvider _services;

        private readonly ObservableAsPropertyHelper<bool> _countriesDbReady, _allowConfigurationChanges;
        private readonly ObservableAsPropertyHelper<string> _exitNodeCountry, _windowTitle;
        private readonly ObservableAsPropertyHelper<ToolbarContent> _ribbonContent;
        private readonly ObservableAsPropertyHelper<IEnumerable<TorNodeCountry>> _onionDbExitCountries;

        public MainWindowViewModel()
        {
            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("This constructor should not be called in a non-design context. Use the other constructor instead.");
            }
        }

        public MainWindowViewModel(TorSession session, IOnionDatabase onionDatabase, IOnionFruitUpdater updater, OnionFruitSettingsStore settings, IServiceProvider services)
        {
            _session = session;
            _settings = settings;
            _services = services;

            var updaterStatus = Observable.FromEventPattern<OnionFruitUpdaterStatus>(h => updater.StatusChanged += h, h => updater.StatusChanged -= h)
                .StartWith(new EventPattern<OnionFruitUpdaterStatus>(this, updater.Status))
                .Select(x => x.EventArgs);

            var updaterProgress = Observable.FromEventPattern<int?>(h => updater.DownloadProgressChanged += h, h => updater.DownloadProgressChanged -= h)
                .StartWith(new EventPattern<int?>(this, updater.DownloadProgress))
                .Select(x => x.EventArgs);

            // configure event-driven observables, ensuring correct disposal of subscriptions
            var sessionState = Observable.FromEventPattern<EventHandler<TorSession.TorSessionState>, TorSession.TorSessionState>(handler => session.SessionStateChanged += handler, handler => session.SessionStateChanged -= handler)
                .StartWith(new EventPattern<TorSession.TorSessionState>(this, session.State))
                .ObserveOn(RxApp.MainThreadScheduler);

            var connectionProgress = Observable.FromEventPattern<EventHandler<int>, int>(handler => session.BootstrapProgressChanged += handler, handler => session.BootstrapProgressChanged -= handler)
                .StartWith(new EventPattern<int>(this, 0))
                .ObserveOn(RxApp.MainThreadScheduler);

            var databaseReady = Observable.FromEventPattern<EventHandler<DatabaseState>, DatabaseState>(handler => onionDatabase.StateChanged += handler, handler => onionDatabase.StateChanged -= handler)
                .StartWith(new EventPattern<DatabaseState>(this, onionDatabase.State))
                .Select(x => x.EventArgs == DatabaseState.Ready)
                .ObserveOn(RxApp.MainThreadScheduler);

            var databaseCountries = Observable.FromEventPattern<EventHandler<IReadOnlyCollection<TorNodeCountry>>, IReadOnlyCollection<TorNodeCountry>>(handler => onionDatabase.CountriesChanged += handler, handler => onionDatabase.CountriesChanged -= handler)
                .StartWith(new EventPattern<IReadOnlyCollection<TorNodeCountry>>(this, onionDatabase.Countries))
                .Select(ProcessCountries)
                .ObserveOn(RxApp.MainThreadScheduler);

            databaseReady.ToProperty(this, x => x.CountriesDatabaseReady, out _countriesDbReady).DisposeWith(_disposables);
            databaseCountries.ToProperty(this, x => x.ExitCountries, out _onionDbExitCountries).DisposeWith(_disposables);

            sessionState.Select(x => x.EventArgs == TorSession.TorSessionState.Disconnected)
                .ToProperty(this, x => x.AllowConfigurationChanges, out _allowConfigurationChanges)
                .DisposeWith(_disposables);

            sessionState
                .CombineLatest(connectionProgress)
                .Select(x => GetRibbonContent(x.First.EventArgs, x.Second.EventArgs))
                .ToProperty(this, x => x.RibbonContent, out _ribbonContent, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(_disposables);

            // don't publish the settings value if the database isn't ready
            settings.GetObservableValue<string>(OnionFruitSetting.TorExitCountryCode)
                .CombineLatest(databaseReady)
                .Where(x => x.Second)
                .Select(x => x.First)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedCountryCode, out _exitNodeCountry)
                .DisposeWith(_disposables);

            updaterStatus.CombineLatest(updaterProgress)
                .Select(GetWindowTitle)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.WindowTitle, out _windowTitle)
                .DisposeWith(_disposables);

            ToggleConnection = ReactiveCommand.CreateFromTask(ToggleSession, this.WhenAnyValue(x => x.RibbonContent).Select(x => x.AllowToggling).ObserveOn(RxApp.MainThreadScheduler)).DisposeWith(_disposables);
            OpenSettingsWindow = ReactiveCommand.CreateFromTask(async () => await SettingsWindowInteraction.Handle(null), sessionState.Select(x => x.EventArgs == TorSession.TorSessionState.Disconnected).ObserveOn(RxApp.MainThreadScheduler)).DisposeWith(_disposables);
        }

        /// <summary>
        /// Command to toggle the connection (i.e. the toggle switch)
        /// </summary>
        public ICommand ToggleConnection { get; }

        /// <summary>
        /// Command to open the settings page
        /// </summary>
        public ICommand OpenSettingsWindow { get; }

        /// <summary>
        /// The current window title (used to display update progress)
        /// </summary>
        public string WindowTitle => _windowTitle.Value;

        /// <summary>
        /// Gets the content of the ribbon (toggle state, text, background colour)
        /// </summary>
        public ToolbarContent RibbonContent => _ribbonContent.Value;

        /// <summary>
        /// Gets the current state of the onion.db information database
        /// </summary>
        public bool CountriesDatabaseReady => _countriesDbReady.Value;

        /// <summary>
        /// Whether interface elements that allow configuration changes should be enabled
        /// </summary>
        public bool AllowConfigurationChanges => _allowConfigurationChanges.Value;

        /// <summary>
        /// The available countries with at least one exit node.
        /// </summary>
        public IEnumerable<TorNodeCountry> ExitCountries => _onionDbExitCountries.Value;

        /// <summary>
        /// Interaction between the current window and a request for the settings page to be opened
        /// </summary>
        public Interaction<string, Unit> SettingsWindowInteraction { get; } = new();

        /// <summary>
        /// The two-letter country code selected to pass traffic out from
        /// </summary>
        public string SelectedCountryCode
        {
            get => _exitNodeCountry.Value;
            set
            {
                // prevent setting null values
                if (value == null)
                {
                    return;
                }

                _settings.SetValue(OnionFruitSetting.TorExitCountryCode, value);
            }
        }

        private async Task ToggleSession()
        {
            if (_session.State is TorSession.TorSessionState.Connecting or TorSession.TorSessionState.Disconnecting)
            {
                return;
            }

            // start session if session is disconnected or null
            if (_session.State is TorSession.TorSessionState.Disconnected)
            {
                // perform any pre-flight checks requested by the platform (this is mainly used on macOS to check onionfruitd is setup)
                var preFlightCheckResult = _services.GetService<ISessionPreFlightCheck>()?.PerformPreFlightCheck();

                if (preFlightCheckResult != null)
                {
                    // open settings window if a tab is specified
                    if (preFlightCheckResult.SettingsTabId != null)
                    {
                        await SettingsWindowInteraction.Handle(preFlightCheckResult.SettingsTabId);
                    }

                    // todo log error as warning
                    return;
                }

                await _session.StartSession();
            }
            else
            {
                await _session.StopSession();
            }
        }

        private static ToolbarContent GetRibbonContent(TorSession.TorSessionState state, int connectionProgress) => state switch
        {
            TorSession.TorSessionState.Disconnected => new ToolbarContent(false, true, new ImmutableSolidColorBrush(Color.FromRgb(244, 67, 54)), "Tor Disconnected"),
            TorSession.TorSessionState.Connected => new ToolbarContent(true, true, Brushes.Green, "Tor Connected"),

            TorSession.TorSessionState.Connecting when connectionProgress == 0 => new ToolbarContent(true, false, Brushes.DarkOrange, "Tor Connecting"),
            TorSession.TorSessionState.Connecting => new ToolbarContent(true, false, Brushes.DarkOrange, $"Tor Connecting ({connectionProgress}%)"),

            TorSession.TorSessionState.ConnectingStalled when connectionProgress == 0 => new ToolbarContent(true, true, Brushes.SlateGray, "Tor Connecting"),
            TorSession.TorSessionState.ConnectingStalled => new ToolbarContent(true, true, Brushes.SlateGray, $"Tor Connecting ({connectionProgress}%)"),

            TorSession.TorSessionState.Disconnecting => new ToolbarContent(false, false, Brushes.DarkOrange, "Tor Disconnecting"),

            TorSession.TorSessionState.BlockedProcess => new ToolbarContent(true, true, Brushes.DarkSlateGray, "Tor process failed to start"),
            TorSession.TorSessionState.BlockedProxy => new ToolbarContent(true, false, Brushes.DarkSlateGray, "Failed to change proxy settings"),

            TorSession.TorSessionState.KillSwitchTriggered => new ToolbarContent(true, true, Brushes.DeepPink, "Tor Process Killed"),

            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        private static IEnumerable<TorNodeCountry> ProcessCountries(EventPattern<IReadOnlyCollection<TorNodeCountry>> countriesEvent)
        {
            if (countriesEvent.EventArgs?.Count is null or 0)
            {
                return [TorNodeCountry.Random];
            }

            return countriesEvent.EventArgs
                .Where(y => y.ExitNodeCount > 0)
                .OrderBy(x => x.CountryName, StringComparer.Ordinal)
                .Prepend(TorNodeCountry.Random);
        }

        private static string GetWindowTitle((OnionFruitUpdaterStatus Status, int? Progress) current) => current.Status switch
        {
            OnionFruitUpdaterStatus.Downloading when current.Progress.HasValue => $"{App.Title} - Downloading Update ({current.Progress}%)",
            OnionFruitUpdaterStatus.Downloading => $"{App.Title} - Downloading Update",
            OnionFruitUpdaterStatus.PendingRestart => $"{App.Title} - Update Downloaded",
            OnionFruitUpdaterStatus.Failed => $"{App.Title} - Update Failed",

            _ => App.Title
        };

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}