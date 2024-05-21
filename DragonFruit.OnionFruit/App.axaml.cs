using System;
using System.Collections.Generic;
using System.Reflection;
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
        Version = Assembly.GetEntryAssembly()?.GetName().Version!.ToString(3);
        Title = $"OnionFruit\u2122 {Version}";

        // enable mica effect on Windows 11 and above
        TransparencyLevels = OperatingSystem.IsWindowsVersionAtLeast(10, 22000) ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur] : [WindowTransparencyLevel.AcrylicBlur];
    }

    public IServiceProvider Services => host.Services;

    public static string Title { get; }
    public static string Version { get; }

    /// <summary>
    /// Transparency level hints passed to windows to enable transparency effects (if supported).
    /// </summary>
    public static readonly IReadOnlyList<WindowTransparencyLevel> TransparencyLevels;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // handle closing event
            desktop.Exit += delegate
            {
                host.StopAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                host.Dispose();

                host = null;
            };
        }

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