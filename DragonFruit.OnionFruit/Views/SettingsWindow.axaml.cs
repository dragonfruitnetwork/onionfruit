// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DragonFruit.OnionFruit.ViewModels;
using FluentAvalonia.UI.Windowing;

namespace DragonFruit.OnionFruit.Views
{
    public partial class SettingsWindow : AppWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();

            TransparencyLevelHint = App.TransparencyLevels;

            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            (DataContext as IDisposable)?.Dispose();
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
}