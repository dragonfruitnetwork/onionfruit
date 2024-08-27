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

        // set titlebar options
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        SelectedTab = Tabs.First();
    }

    public SettingsTabInfo SelectedTab
    {
        get => GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    public IEnumerable<SettingsTabInfo> Tabs { get; } =
    [
        new("Connection", Symbol.Globe, () => new ConnectionSettingsTabView
        {
            DataContext = ActivatorUtilities.CreateInstance<ConnectionSettingsTabViewModel>(App.Instance.Services)
        })
    ];

    public IEnumerable<SettingsTabInfo> FooterTabs { get; } =
    [
        new("About OnionFruit", Symbol.Go, () => new ContentPresenter())
    ];
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