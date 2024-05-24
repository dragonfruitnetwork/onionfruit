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
        private readonly IOnionDatabase _onionDatabase;
        private readonly OnionFruitSettingsStore _settings;

        private readonly ObservableAsPropertyHelper<bool> _countriesDbReady, _allowConfigurationChanges;
        private readonly ObservableAsPropertyHelper<string> _exitNodeCountry;
        private readonly ObservableAsPropertyHelper<ToolbarContent> _ribbonContent;
        private readonly ObservableAsPropertyHelper<IEnumerable<TorNodeCountry>> _onionDbExitCountries;

        public MainWindowViewModel()
        {
            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("This constructor should not be called in a non-design context. Use the other constructor instead.");
            }
        }

        public MainWindowViewModel(TorSession session, IOnionDatabase onionDatabase, OnionFruitSettingsStore settings)
        {
            _session = session;
            _onionDatabase = onionDatabase;
            _settings = settings;

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

            _countriesDbReady = databaseReady.ToProperty(this, x => x.CountriesDatabaseReady).DisposeWith(_disposables);
            _allowConfigurationChanges = sessionState.Select(x => x.EventArgs == TorSession.TorSessionState.Disconnected).ToProperty(this, x => x.AllowConfigurationChanges).DisposeWith(_disposables);

            _onionDbExitCountries = databaseCountries
                .CombineLatest(databaseReady)
                .Where(x => x.Second)
                .Select(x => x.First)
                .ToProperty(this, x => x.ExitCountries)
                .DisposeWith(_disposables);

            _ribbonContent = sessionState
                .CombineLatest(connectionProgress)
                .Select(x => GetRibbonContent(x.First.EventArgs, x.Second.EventArgs))
                .ToProperty(this, x => x.RibbonContent, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(_disposables);

            _exitNodeCountry = settings.GetObservableValue<string>(OnionFruitSetting.TorExitCountryCode)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedCountryCode)
                .DisposeWith(_disposables);

            ToggleConnection = ReactiveCommand.CreateFromTask(ToggleSession, this.WhenAnyValue(x => x.RibbonContent).Select(x => x.AllowToggling).ObserveOn(RxApp.MainThreadScheduler)).DisposeWith(_disposables);
        }

        /// <summary>
        /// Command to toggle the connection (i.e. the toggle switch)
        /// </summary>
        public ICommand ToggleConnection { get; }

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
        /// The two-letter country code selected to pass traffic out from
        /// </summary>
        public string SelectedCountryCode
        {
            get => _exitNodeCountry.Value;
            set
            {
                if (_onionDatabase.State != DatabaseState.Ready)
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
                await _session.StartSession();
            }
            else
            {
                await _session.StopSession();
            }
        }

        private ToolbarContent GetRibbonContent(TorSession.TorSessionState state, int connectionProgress) => state switch
        {
            TorSession.TorSessionState.Disconnected => new ToolbarContent(false, true, new ImmutableSolidColorBrush(Color.FromRgb(244, 67, 54)), "Tor Disconnected"),
            TorSession.TorSessionState.Connected => new ToolbarContent(true, true, Brushes.Green, "Tor Connected"),

            TorSession.TorSessionState.Connecting when connectionProgress == 0 => new ToolbarContent(true, false, Brushes.DarkOrange, "Tor Connecting"),
            TorSession.TorSessionState.Connecting => new ToolbarContent(true, false, Brushes.DarkOrange, $"Tor Connecting ({connectionProgress}%)"),

            TorSession.TorSessionState.ConnectingStalled when connectionProgress == 0 => new ToolbarContent(true, true, Brushes.SlateGray, "Tor Connecting"),
            TorSession.TorSessionState.ConnectingStalled => new ToolbarContent(true, true, Brushes.SlateGray, $"Tor Connecting ({connectionProgress}%)"),

            TorSession.TorSessionState.Disconnecting => new ToolbarContent(false, false, Brushes.DarkOrange, "Tor Disconnecting"),

            TorSession.TorSessionState.BlockedProcess => new ToolbarContent(false, false, Brushes.Black, "Tor Process blocked from starting"),
            TorSession.TorSessionState.BlockedProxy => new ToolbarContent(false, false, Brushes.Black, "OnionFruit was blocked from changing proxy settings"),

            TorSession.TorSessionState.KillSwitchTriggered => new ToolbarContent(true, true, Brushes.DeepPink, "Tor Process Killed (Killswitch enabled)"),

            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        private static IEnumerable<TorNodeCountry> ProcessCountries(EventPattern<IReadOnlyCollection<TorNodeCountry>> countriesEvent)
        {
            if (countriesEvent.EventArgs?.Count is null or 0)
            {
                return Enumerable.Empty<TorNodeCountry>();
            }

            uint entry = 0, exit = 0, total = 0;

            foreach (var country in countriesEvent.EventArgs)
            {
                entry += country.EntryNodeCount;
                exit += country.ExitNodeCount;
                total += country.TotalNodeCount;
            }

            return countriesEvent.EventArgs
                .Where(y => y.ExitNodeCount > 0)
                .OrderBy(x => x.CountryName, StringComparer.Ordinal)
                .Prepend(new TorNodeCountry("Random", IOnionDatabase.TorCountryCode, entry, exit, total));
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}