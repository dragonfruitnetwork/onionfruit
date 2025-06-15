using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.ViewModels.Interfaces;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace DragonFruit.OnionFruit.Views
{
    public partial class MainWindow : ReactiveAppWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            // set titlebar options
            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

            TransparencyLevelHint = App.TransparencyLevels;

            this.WhenActivated(action => action(ViewModel!.SettingsWindowInteraction.RegisterHandler(OpenSettingsWindow)));
        }

        private async Task OpenSettingsWindow(IInteractionContext<string, Unit> ctx)
        {
            var viewModel = App.Instance.Services.GetRequiredService<SettingsWindowViewModel>();

            if (!string.IsNullOrEmpty(ctx.Input))
            {
                viewModel.SetActiveTab(ctx.Input);
            }

            var window = new SettingsWindow() {DataContext = viewModel};

            await window.ShowDialog(this);
            ctx.SetOutput(Unit.Default);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is IHasCustomStartupPosition position)
            {
                Position = position.GetInitialPosition(Screens.Primary, ClientSize);
            }
        }

        private void HandleCloseRequest(object sender, WindowClosingEventArgs e)
        {
            if (e.CloseReason != WindowCloseReason.WindowClosing)
            {
                return;
            }

            _ = App.Instance.RequestAppShutdown();

            // block default close behavior
            e.Cancel = true;
        }
    }
}