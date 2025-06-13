// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO;
using System.Runtime.InteropServices;
using AppServiceSharp;
using Avalonia;
using Avalonia.ReactiveUI;
using DragonFruit.Data;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core;
using DragonFruit.OnionFruit.Core.MacOS;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.MacOS.ViewModels;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.Services;
using DragonFruit.OnionFruit.Updater;
using DragonFruit.OnionFruit.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Velopack;

namespace DragonFruit.OnionFruit.MacOS
{
    public static class Program
    {
#if !DEBUG
        private const string XpcServiceName = "network.dragonfruit.onionfruit.xpc";
        private const string DaemonPlistName = "onionfruitd.plist";
#else
        private const string XpcServiceName = "network.dragonfruit.onionfruit.xpc-dev";
        private const string DaemonPlistName = null;
#endif

        private static IHost _host;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            // requires macOS 13 or later for SMAppService daemon installation support
            if (!OperatingSystem.IsMacOSVersionAtLeast(13))
            {
                MacOSMessageBox.Show("Unsupported macOS Version", "OnionFruit\u2122 requires macOS 13 or later to run. Please update your system and try again.");
                return;
            }

            var fileLog = Path.Combine(App.StoragePath, "logs", "runtime.log");
            if (File.Exists(fileLog))
            {
                using var stream = File.OpenWrite(fileLog);
                stream.SetLength(0);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(fileLog, LogEventLevel.Debug)
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

                    o.DisableUnobservedTaskExceptionCapture();
                })
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += PerformFatalCrashShutdown;

            _host = BuildHost();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App(_host ?? BuildHost()))
            .UsePlatformDetect()
            .UseReactiveUI();

        private static IHost BuildHost() => Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
            })
            .ConfigureServices((context, services) =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!string.IsNullOrEmpty(DaemonPlistName))
                {
                    services.AddKeyedSingleton("DaemonAppService", (_, _) => AppService.DaemonServiceWithPlistName(DaemonPlistName));
                    services.AddTransient<ISessionPreFlightCheck, MacOSPreflightCheck>();
                }

                services.AddSingleton<INetworkAdapterManager, MacOSNetworkServiceManager>(s => new MacOSNetworkServiceManager(XpcServiceName, s.GetKeyedService<AppService>("DaemonAppService")));
                services.AddSingleton<ExecutableLocator, MacOSExecutableLocator>();

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

                services.AddSingleton<IProcessElevator, MacOSAppInstanceManager>();
                services.AddSingleton<IStartupLaunchService, MacOSLaunchItemService>();

                services.AddHostedService<DiscordRpcService>();
                services.AddHostedService<LandingPageLaunchService>();
                services.AddHostedService(s => s.GetRequiredService<OnionDbService>());
                services.AddHostedService(s => (VelopackUpdater)s.GetRequiredService<IOnionFruitUpdater>());

                // register view models
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<SettingsWindowViewModel, MacOSSettingsWindowViewModel>();
            }).Build();

        private static UpdateOptions GetUpdateOptions(OnionFruitSettingsStore settings)
        {
            var targetStream = settings.GetValue<UpdateStream?>(OnionFruitSetting.ExplicitUpdateStream);
            var channelName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "mac-arm64" : "mac-x64";

            return new UpdateOptions
            {
                AllowVersionDowngrade = true,
                ExplicitChannel = VelopackUpdater.UpdateChannelName(channelName, targetStream)
            };
        }

        private static void PerformFatalCrashShutdown(object sender, UnhandledExceptionEventArgs eventArgs)
        {
            if (!eventArgs.IsTerminating)
            {
                return;
            }

            // create a marker file to indicate that the application crashed
            File.Create(Path.Combine(App.StoragePath, ".app-crash")).Dispose();
            Log.Logger.Fatal("Unhandled exception: {message}", (eventArgs.ExceptionObject as Exception)?.Message);

            MacOSMessageBox.Show(
                "Application Crash",
                "OnionFruit\u2122 has encountered an unrecoverable error and must close. After clicking OK, Tor will attempt to disconnect and the application will close.");

            // shutdown any ongoing session
            _host?.Services.GetService<TorSession>().StopSession().AsTask().Wait();
        }
    }
}