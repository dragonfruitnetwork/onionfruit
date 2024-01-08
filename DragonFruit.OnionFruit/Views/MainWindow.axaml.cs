using Avalonia.Interactivity;
using DragonFruit.OnionFruit.ViewModels.Interfaces;
using FluentAvalonia.UI.Windowing;

namespace DragonFruit.OnionFruit.Views;

public partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // set titlebar options
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is IHasCustomStartupPosition position)
        {
            Position = position.GetInitialPosition(Screens.Primary, ClientSize);
        }
    }
}