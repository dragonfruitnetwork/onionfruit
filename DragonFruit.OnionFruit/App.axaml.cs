using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Views;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nito.AsyncEx;

namespace DragonFruit.OnionFruit;

public partial class App(IHost host) : Application
{
    internal static string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonFruit Network", "OnionFruit");

    private readonly AsyncManualResetEvent _shutdownSignal = new(true);
    private readonly AsyncSemaphore _shutdownQueue = new(1);

    private IDisposable _startupCallback;
    private CancellationTokenSource _shutdownSignalCancellation;

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
    public IServiceProvider Services => host.Services;

    public static string Title { get; }
    public static string Version { get; }

    /// <summary>
    /// Transparency level hints passed to windows to enable transparency effects (if supported).
    /// </summary>
    public static IReadOnlyList<WindowTransparencyLevel> TransparencyLevels { get; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        var hostLifetime = Services.GetRequiredService<IHostApplicationLifetime>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => host.StopAsync().Wait();
        }

        // because background services need to be started, StartAsync blocks until the app closes.
        // using the IHostApplicationLifetime, we can be notified when the windows are ready to be shown.
        _startupCallback = hostLifetime.ApplicationStarted.Register(StartAvaloniaApp);

        _ = host.StartAsync();
    }

    private void StartAvaloniaApp()
    {
        // release callback handler
        _startupCallback.Dispose();
        _startupCallback = null;

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            throw new InvalidOperationException("Cannot start when the application is not running in desktop mode.");
        }

        Services.GetRequiredService<TorSession>().SessionStateChanged += (_, state) =>
        {
            // this will need updating when the updater is implemented
            switch (state)
            {
                case TorSession.TorSessionState.KillSwitchTriggered:
                    ActivateApp();
                    break;

                case TorSession.TorSessionState.BlockedProcess:
                case TorSession.TorSessionState.BlockedProxy:
                case TorSession.TorSessionState.Disconnected:
                    _shutdownSignal.Set();
                    break;

                default:
                    _shutdownSignal.Reset();
                    break;
            }
        };

        desktop.MainWindow = new MainWindow
        {
            ViewModel = Services.GetRequiredService<MainWindowViewModel>()
        };
    }

    internal async Task RequestAppShutdown()
    {
        // prevent multiple shutdown requests from being queued
        // this should never fail, but if it does the app will be forced to close
        using (var queueTimeout = new CancellationTokenSource(250))
        {
            await _shutdownQueue.WaitAsync(queueTimeout.Token);
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            throw new InvalidOperationException("Cannot request shutdown when the application is not running in desktop mode.");
        }

        if (desktop.MainWindow != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                desktop.MainWindow.ShowInTaskbar = false;
                desktop.MainWindow.IsVisible = false;
            });
        }

        // Closing should only occur when the user has closed the main window, is not connected and the updater is not running.
        // If any of these conditions are not satisfied, the main window should be hidden and the app should wait on the signal before closing.
        // Additionally, the signal should have a cancellation token attached to allow cancellation of the shutdown process.

        _shutdownSignalCancellation?.Dispose();
        _shutdownSignalCancellation = new CancellationTokenSource();

        try
        {
            // show the tray icon if we're 500ms into waiting and nothing has happened
            var waitTask = _shutdownSignal.WaitAsync(_shutdownSignalCancellation.Token);
            _ = waitTask.WaitAsync(TimeSpan.FromMilliseconds(500)).ContinueWith(t =>
            {
                if (_shutdownSignalCancellation.IsCancellationRequested || t.Exception?.InnerException?.GetType().IsAssignableTo(typeof(TimeoutException)) != true)
                {
                    return;
                }

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var trayIcon = TrayIcon.GetIcons(this)?.SingleOrDefault();
                    if (trayIcon != null)
                    {
                        trayIcon.IsVisible = true;
                    }
                });
            }, TaskContinuationOptions.OnlyOnFaulted);

            await waitTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // release the semaphore if the shutdown was cancelled
            _shutdownQueue.Release();
            return;
        }

        desktop.Shutdown();
    }

    public void ActivateApp()
    {
        var window = ((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime)!.MainWindow;
        if (window == null)
        {
            return;
        }

        // cancel any pending shutdowns
        _shutdownSignalCancellation?.Cancel();

        window.WindowState = WindowState.Normal;
        window.ShowInTaskbar = true;
        window.IsVisible = true;

        var trayIcon = TrayIcon.GetIcons(this)?.SingleOrDefault();
        if (trayIcon != null)
        {
            trayIcon.IsVisible = false;
        }

        window.Activate();
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
