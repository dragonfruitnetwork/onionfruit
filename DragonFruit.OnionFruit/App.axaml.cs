using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DragonFruit.OnionFruit;

public partial class App(IHost host) : Application
{
    private IDisposable _startupCallback;

    public static App Instance => (App)Current;

    static App()
    {
        Version = typeof(App).Assembly.GetName().Version!.ToString(3);
        Title = $"OnionFruit\u2122 {Version}";
    }

    public IServiceProvider Services => host.Services;

    public static string Title { get; }
    public static string Version { get; }

    public static readonly IReadOnlyList<WindowTransparencyLevel> TransparencyLevels = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        // setup host shutdown logic
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += delegate
            {
                host.StopAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                host.Dispose();

                host = null;
            };
        }

        // DataTemplates.Add(Host.Services.GetRequiredService<ViewLocator>());

        // because background services need to be started, StartAsync blocks until the app closes.
        // using the IHostApplicationLifetime, we can be notified when the windows are ready to be shown.
        _startupCallback = Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(StartApp);

        _ = host.StartAsync();
    }

    private void StartApp()
    {
        // release callback handler
        _startupCallback.Dispose();

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        desktop.MainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainWindowViewModel>()
        };
    }
}