using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia;
using Avalonia.ReactiveUI;
using DragonFruit.Data;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Core.Windows;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.Rpc;
using DragonFruit.OnionFruit.Services;
using DragonFruit.OnionFruit.Updater;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Windows.Rpc;
using DragonFruit.OnionFruit.Windows.ViewModels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Velopack;

namespace DragonFruit.OnionFruit.Windows;

public static class Program
{
    private static IHost _host;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        HandleSecondInstance();

        VelopackApp.Build().Run();

        // standard application startup
        var fileLog = Path.Combine(App.StoragePath, "logs", "runtime.log");
        if (File.Exists(fileLog))
        {
            using var stream = File.OpenWrite(fileLog);
            stream.SetLength(0);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(fileLog, LogEventLevel.Debug)
            .WriteTo.EventLog("OnionFruit", "Application", restrictedToMinimumLevel: LogEventLevel.Error)
            .WriteTo.Console(LogEventLevel.Debug, theme: AnsiConsoleTheme.Literate)
            .WriteTo.Sentry(o =>
            {
                o.Dsn = "https://f63ab85d7581988829e9f47d329d83d5@o97031.ingest.us.sentry.io/4508002219917312";

                o.MaxBreadcrumbs = 100;
                o.SendDefaultPii = false;
                o.Release = typeof(App).Assembly.GetName().Version!.ToString(3);

#if DEBUG
                o.SetBeforeSend(_ => null);
#else
                // enable error reporting only in release builds and when the user hasn't opted out.
                // launch failures are always reported as settings can't be loaded to check if the user has opted out.
                o.SetBeforeSend(e => App.Instance.Services?.GetService<OnionFruitSettingsStore>()?.GetValue<bool>(OnionFruitSetting.EnableErrorReporting) == false ? null : e);
#endif

                o.MinimumEventLevel = LogEventLevel.Error;
                o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
            })
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += PerformShutdown;

        _host = BuildHost();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App(_host ?? BuildHost()))
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI();

    private static IHost BuildHost() => Host.CreateDefaultBuilder()
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog();
        })
        .ConfigureServices((context, services) =>
        {
            // register platform-specific services
            services.AddSingleton<IProxyManager, WindowsProxyManager>();
            services.AddSingleton<ExecutableLocator>(new WindowsExecutableLocator("ONIONFRUIT_HOME"));

            // configuration
            services.AddSingleton<OnionFruitSettingsStore>();

            // register core services
            services.AddSingleton<TorSession>();
            services.AddSingleton<OnionDbService>();
            services.AddSingleton<TransportManager>();
            services.AddSingleton<ApiClient, OnionFruitClient>();
            services.AddSingleton<IOnionDatabase>(s => s.GetRequiredService<OnionDbService>());
            services.AddSingleton<IOnionFruitUpdater>(s =>
            {
                var settings = s.GetRequiredService<OnionFruitSettingsStore>();
                return ActivatorUtilities.CreateInstance<VelopackUpdater>(s, GetUpdateOptions(settings));
            });

            services.AddHostedService<OnionRpcServer>();
            services.AddHostedService<DiscordRpcService>();
            services.AddHostedService<LandingPageLaunchService>();
            services.AddHostedService(s => s.GetRequiredService<OnionDbService>());
            services.AddHostedService(s => (VelopackUpdater)s.GetRequiredService<IOnionFruitUpdater>());

            // register view models
            services.AddTransient<MainWindowViewModel, Win32MainWindowViewModel>();
        }).Build();

    private static void HandleSecondInstance()
    {
        var channel = new NamedPipeChannel(".", OnionRpcServer.RpcPipeName);
        var client = new OnionRpc.OnionRpcClient(channel);

        try
        {
            var response = client.SecondInstanceLaunched(new Empty(), deadline: DateTime.UtcNow.AddSeconds(2));
            if (response.HasWaitForPidExit)
            {
                using var process = Process.GetProcessById(response.WaitForPidExit);
                process.WaitForExit();
            }

            if (response.ShouldClose)
            {
                Environment.Exit(0);
            }
        }
        catch (RpcException)
        {
            // do nothing
        }
        catch (TimeoutException)
        {
            // do nothing
        }
    }

    private static UpdateOptions GetUpdateOptions(OnionFruitSettingsStore settings)
    {
        var targetStream = settings.GetValue<UpdateStream?>(OnionFruitSetting.ExplicitUpdateStream);
        var prefix = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => null,

            _ => throw new PlatformNotSupportedException()
        };

        return new UpdateOptions
        {
            AllowVersionDowngrade = true,
            ExplicitChannel = VelopackUpdater.UpdateChannelName(prefix, targetStream)
        };
    }

    private static void PerformShutdown(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (!eventArgs.IsTerminating)
        {
            return;
        }

        // create a marker file to indicate that the application crashed
        File.Create(Path.Combine(App.StoragePath, ".app-crash")).Dispose();
        Log.Logger.Fatal("Unhandled exception: {message}", (eventArgs.ExceptionObject as Exception)?.Message);

        PInvoke.ShellMessageBox(SafeAccessTokenHandle.InvalidHandle,
            HWND.Null,
            "OnionFruit has encountered an unrecoverable error and must close. After clicking OK, Tor will attempt to disconnect gracefully and the application will close.",
            "OnionFruit",
            MESSAGEBOX_STYLE.MB_OK);

        // shutdown any ongoing session
        _host?.Services.GetService<TorSession>().StopSession().AsTask().Wait();
    }
}