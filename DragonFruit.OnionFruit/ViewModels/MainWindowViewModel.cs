// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
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
    public record ToolbarContent(bool ToggleChecked, bool AllowToggling, Color Background, string Text);

    public class MainWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly CompositeDisposable _disposables = new();
        private readonly TorSession _session;

        private readonly ObservableAsPropertyHelper<ToolbarContent> _ribbonContent;

        public MainWindowViewModel()
        {
            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("This constructor should not be called in a non-design context. Use the other constructor instead.");
            }
        }

        public MainWindowViewModel(TorSession session)
        {
            _session = session;

            // setup session event pump
            var sessionState = Observable.FromEventPattern<EventHandler<TorSession.TorSessionState>, TorSession.TorSessionState>(handler => session.SessionStateChanged += handler, handler => session.SessionStateChanged -= handler)
                .StartWith(new EventPattern<TorSession.TorSessionState>(this, session.State))
                .ObserveOn(RxApp.MainThreadScheduler);

            var ribbonContentSelector = sessionState.Select(x => GetRibbonContent(x.EventArgs));

            // setup ribbon content
            _ribbonContent = ribbonContentSelector.ToProperty(this, x => x.RibbonContent, scheduler: RxApp.MainThreadScheduler);
            ribbonContentSelector.Subscribe().DisposeWith(_disposables);

            // in the future, there should be a way to move this elsewhere
            ToggleConnection = ReactiveCommand.CreateFromTask(ToggleSession, this.WhenAnyValue(x => x.RibbonContent).ObserveOn(RxApp.MainThreadScheduler).Select(x => x.AllowToggling), RxApp.TaskpoolScheduler);
        }

        /// <summary>
        /// Command to toggle the connection (i.e. the toggle switch)
        /// </summary>
        public ICommand ToggleConnection { get; }

        /// <summary>
        /// Gets the content of the ribbon (toggle state, text, background colour)
        /// </summary>
        public ToolbarContent RibbonContent => _ribbonContent.Value;

        private async Task ToggleSession()
        {
            if (_session?.State is null or TorSession.TorSessionState.Connecting or TorSession.TorSessionState.Disconnecting)
            {
                // todo log warning?
                return;
            }

            // start session if session is disconnected or null
            if (_session.State is TorSession.TorSessionState.Disconnected)
            {
                await _session.StartSession();
            }
            else
            {
                _session.StopSession();
            }
        }

        private ToolbarContent GetRibbonContent(TorSession.TorSessionState state) => state switch
        {
            TorSession.TorSessionState.Disconnected => new ToolbarContent(false, true, Colors.Red, "Tor Disconnected"),
            TorSession.TorSessionState.Connected => new ToolbarContent(true, true, Colors.Green, "Tor Connected"),

            TorSession.TorSessionState.Connecting => new ToolbarContent(false, false, Colors.DarkOrange, "Tor Connecting"),
            TorSession.TorSessionState.ConnectingStalled => new ToolbarContent(false, true, Colors.SlateGray, "Tor Connecting"),

            TorSession.TorSessionState.Disconnecting => new ToolbarContent(false, false, Colors.DarkOrange, "Tor Disconnecting"),

            TorSession.TorSessionState.BlockedProcess => new ToolbarContent(false, false, Colors.Black, "Tor Process blocked from starting"),
            TorSession.TorSessionState.BlockedProxy => new ToolbarContent(false, false, Colors.Black, "OnionFruit was blocked from changing proxy settings"),

            TorSession.TorSessionState.KillSwitchTriggered => new ToolbarContent(true, true, Colors.DeepPink, "Tor Process Killed (Killswitch enabled)"),

            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}