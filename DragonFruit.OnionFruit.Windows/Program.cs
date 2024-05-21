using System;
using Avalonia;
using Avalonia.ReactiveUI;
using DragonFruit.Data;
using DragonFruit.OnionFruit.Core;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Core.Windows;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.Services;
using DragonFruit.OnionFruit.Services.OnionDatabase;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Windows.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Windows;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App(BuildHost()))
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .UseReactiveUI();

    private static IHost BuildHost() => Host.CreateDefaultBuilder()
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.IncludeScopes = false;
                o.TimestampFormat = "[dd/MM/yyyy hh:mm:ss] ";
            });

            logging.AddEventLog(o =>
            {
                o.Filter = (_, level) => level > LogLevel.Information;
                o.SourceName = $"OnionFruit/v{typeof(Program).Assembly.GetName().Version!.ToString(3)}";
            });
        })
        .ConfigureServices((context, services) =>
        {
            // common services
            services.AddSingleton<ApiClient, OnionFruitClient>();

            // register platform-specific services
            services.AddSingleton<IProxyManager, WindowsProxyManager>();
            services.AddSingleton<ExecutableLocator>(new WindowsExecutableLocator("ONIONFRUIT_HOME"));

            // register core services and background tasks
            services.AddHostedService<OnionDbService>();

            services.AddSingleton<TorSession>();
            services.AddSingleton<IOnionDatabase>(s => s.GetRequiredService<OnionDbService>());

            // register view models
            services.AddTransient<MainWindowViewModel, Win32MainWindowViewModel>();
        })
        .Build();
}