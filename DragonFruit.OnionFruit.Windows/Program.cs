using System;
using Avalonia;
using Avalonia.ReactiveUI;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Windows.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => new App(BuildHost()))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // register view models
                services.AddTransient<MainWindowViewModel, Win32MainWindowViewModel>();
            })
            .Build();
}