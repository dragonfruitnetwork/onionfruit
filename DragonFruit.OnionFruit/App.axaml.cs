using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Views;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DragonFruit.OnionFruit;

public partial class App(IHost host) : Application
{
    private IDisposable _startupCallback;
    private IHost _host = host;

    internal static string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonFruit Network", "OnionFruit");

    static App()
    {
        Directory.CreateDirectory(StoragePath);

        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;
        Version = assemblyVersion!.ToString(assemblyVersion.Minor > 0 ? 3 : 2);

#if DEBUG
        Title = "OnionFruit\u2122 Development Edition";
#else
        Title = $"OnionFruit\u2122";
#endif

        // enable mica effect on Windows 11 and above
        TransparencyLevels = OperatingSystem.IsWindowsVersionAtLeast(10, 22000) ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur] : [WindowTransparencyLevel.AcrylicBlur];
    }

    public static App Instance => (App)Current;
    public IServiceProvider Services => _host.Services;

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
                _host.StopAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                _host.Dispose();

                _host = null;
            };
        }

        // because background services need to be started, StartAsync blocks until the app closes.
        // using the IHostApplicationLifetime, we can be notified when the windows are ready to be shown.
        _startupCallback = Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(StartApp);

        _ = _host.StartAsync();
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
            ViewModel = Services.GetRequiredService<MainWindowViewModel>()
        };
    }

    /// <summary>
    /// Launches a URL in the default browser
    /// </summary>
    /// <param name="url">The url to launch</param>
    /// <returns>Whether the request was completed successfully</returns>
    public static bool Launch(string url)
    {
        var psi = new ProcessStartInfo
        {
            Verb = "open",
            FileName = url,
            UseShellExecute = true
        };

        return Process.Start(psi) != null;
    }

    public static IconSource GetIcon(LucideIconNames icon, IImmutableSolidColorBrush brush = null, double thickness = 1.5)
    {
        var resource = Instance.Resources.MergedDictionaries.FirstOrDefault() as ResourceDictionary;
        var drawingImage = resource?[icon.ToString()] as DrawingImage;

        // set the icon color to white
        foreach (var drawing in (drawingImage?.Drawing as DrawingGroup)?.Children ?? [])
        {
            if (drawing is GeometryDrawing {Pen: Pen pen})
            {
                pen.Brush = brush ?? Brushes.White;
                pen.Thickness = thickness;
            }
        }

        return new ImageIconSource
        {
            Source = drawingImage
        };
    }
}