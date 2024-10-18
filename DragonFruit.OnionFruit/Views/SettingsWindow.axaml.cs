// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.Views.Settings;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using LucideAvalonia.Enum;
using Microsoft.Extensions.DependencyInjection;

namespace DragonFruit.OnionFruit.Views;

// XAML can't have nested classes
public record SettingsTabInfo(string Name, IconSource Icon, Func<Control> ContentFactory);

public partial class SettingsWindow : AppWindow
{
    public static readonly StyledProperty<SettingsTabInfo> SelectedTabProperty = AvaloniaProperty.Register<SettingsWindow, SettingsTabInfo>(nameof(SelectedTab), defaultValue: null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable<SettingsTabInfo>> TabsProperty = AvaloniaProperty.Register<SettingsWindow, IEnumerable<SettingsTabInfo>>(nameof(Tabs), defaultValue: [], defaultBindingMode: BindingMode.OneWay);
    public static readonly StyledProperty<IEnumerable<SettingsTabInfo>> FooterTabsProperty = AvaloniaProperty.Register<SettingsWindow, IEnumerable<SettingsTabInfo>>(nameof(FooterTabs), defaultValue: [], defaultBindingMode: BindingMode.OneWay);

    public SettingsWindow()
    {
        InitializeComponent();

        TransparencyLevelHint = App.TransparencyLevels;

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        Tabs =
        [
            new("Connection", App.GetIcon(LucideIconNames.EthernetPort), () => new ConnectionSettingsTabView
            {
                DataContext = ActivatorUtilities.CreateInstance<ConnectionSettingsTabViewModel>(App.Instance.Services)
            }),
            new("Landing Pages", App.GetIcon(LucideIconNames.Chrome), () => new LandingPageSettingsTabView
            {
                DataContext = ActivatorUtilities.CreateInstance<LandingPageSettingsTabViewModel>(App.Instance.Services)
            }),
            new("Bridges", App.GetIcon(LucideIconNames.Castle), () => new BridgeSettingsTabView
            {
                DataContext = ActivatorUtilities.CreateInstance<BridgeSettingsTabViewModel>(App.Instance.Services)
            }),
            new("External Connections", App.GetIcon(LucideIconNames.Sparkles), () => new ExternalConnectionsSettingsTabView
            {
                DataContext = ActivatorUtilities.CreateInstance<ExternalConnectionsSettingsTabViewModel>(App.Instance.Services)
            })
        ];

        FooterTabs =
        [
            new("About OnionFruit", App.GetIcon(LucideIconNames.Info), () => new AboutPageTabView
            {
                DataContext = ActivatorUtilities.CreateInstance<AboutPageTabViewModel>(App.Instance.Services)
            })
        ];

        SelectedTab = Tabs.First();
    }

    public IDataTemplate TabTemplate { get; } = new SettingTabViewTemplate();

    public SettingsTabInfo SelectedTab
    {
        get => GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    public IEnumerable<SettingsTabInfo> Tabs
    {
        get => GetValue(TabsProperty);
        private set => SetValue(TabsProperty, value);
    }

    public IEnumerable<SettingsTabInfo> FooterTabs
    {
        get => GetValue(FooterTabsProperty);
        private set => SetValue(FooterTabsProperty, value);
    }

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