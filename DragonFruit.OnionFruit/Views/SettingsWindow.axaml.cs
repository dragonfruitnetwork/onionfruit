// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Views.Settings;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace DragonFruit.OnionFruit.Views;

// XAML can't have nested classes
public record SettingsTabInfo(string Name, Symbol Icon, Func<Control> ContentFactory);

public partial class SettingsWindow : AppWindow
{
    private static readonly StyledProperty<SettingsTabInfo> SelectedTabProperty = AvaloniaProperty.Register<SettingsWindow, SettingsTabInfo>(nameof(SelectedTab), defaultValue: null, defaultBindingMode: BindingMode.TwoWay);

    public SettingsWindow()
    {
        InitializeComponent();

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        TransparencyLevelHint = App.TransparencyLevels;

        SelectedTab = Tabs.First();
    }

    public SettingsTabInfo SelectedTab
    {
        get => GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    public IDataTemplate TabTemplate { get; } = new SettingTabViewTemplate();

    public IEnumerable<SettingsTabInfo> Tabs { get; } =
    [
        new SettingsTabInfo("Connection", Symbol.Globe, () => new ConnectionSettingsTabView
        {
            DataContext = ActivatorUtilities.CreateInstance<ConnectionSettingsTabViewModel>(App.Instance.Services)
        }),
        new SettingsTabInfo("Landing Pages", Symbol.Go, () => new LandingPageSettingsTabView
        {
            DataContext = ActivatorUtilities.CreateInstance<LandingPageSettingsTabViewModel>(App.Instance.Services)
        }),
        new SettingsTabInfo("Bridges", Symbol.Link, () => new BridgeSettingsTabView
        {
            DataContext = ActivatorUtilities.CreateInstance<BridgeSettingsTabViewModel>(App.Instance.Services)
        }),
    ];

    public IEnumerable<SettingsTabInfo> FooterTabs { get; } =
    [
        new SettingsTabInfo("About OnionFruit", Symbol.Help, () => new ContentPresenter())
    ];

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        (TabTemplate as IDisposable)?.Dispose();
    }
}

public class SettingTabViewTemplate : IDataTemplate, IDisposable
{
    private IDisposable _lastViewModel;

    public Control Build(object param)
    {
        var tabInfo = (SettingsTabInfo)param;
        var control = tabInfo.ContentFactory.Invoke();

        control.Margin = new Thickness(15);
        control = new ScrollViewer
        {
            Content = control
        };

        _lastViewModel?.Dispose();
        _lastViewModel = control.DataContext as IDisposable;

        return control;
    }

    public bool Match(object data) => data is SettingsTabInfo;

    public void Dispose()
    {
        _lastViewModel?.Dispose();
    }
}