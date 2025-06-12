// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DragonFruit.OnionFruit.Views;
using DragonFruit.OnionFruit.Views.Settings;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    // XAML can't have nested classes
    public record SettingsTabInfo(string Name, IconSource Icon, Func<Control> ContentFactory);

    public class SettingsWindowViewModel : ReactiveObject, IDisposable
    {
        private SettingsTabInfo _selectedTab;

        public SettingsWindowViewModel()
        {
            Tabs.AddRange(
            [
                Tab<ConnectionSettingsTabView, ConnectionSettingsTabViewModel>("Connection", LucideIconNames.EthernetPort),
                Tab<BridgeSettingsTabView, BridgeSettingsTabViewModel>("Bridges", LucideIconNames.Castle),
                Tab<DnsPageTabView, DnsPageTabViewModel>("DNS", LucideIconNames.Signpost),
                Tab<LandingPageSettingsTabView, LandingPageSettingsTabViewModel>("Landing Pages", LucideIconNames.Chrome),
                Tab<ExternalConnectionsSettingsTabView, ExternalConnectionsSettingsTabViewModel>("External Connections", LucideIconNames.Sparkles)
            ]);

            FooterTabs.Add(Tab<AboutPageTabView, AboutPageTabViewModel>("About OnionFruit™", LucideIconNames.Info));
        }

        public SettingsTabInfo SelectedTab
        {
            get => _selectedTab ??= Tabs.FirstOrDefault();
            set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
        }

        public IDataTemplate TabTemplate { get; } = new SettingTabViewTemplate();
        public IObservableCollection<SettingsTabInfo> Tabs { get; } = new ObservableCollectionExtended<SettingsTabInfo>();
        public IObservableCollection<SettingsTabInfo> FooterTabs { get; } = new ObservableCollectionExtended<SettingsTabInfo>();

        public void Dispose()
        {
            (TabTemplate as IDisposable)?.Dispose();
        }

        protected static SettingsTabInfo Tab<TTab, TTabViewModel>(string name, LucideIconNames icon) where TTab : UserControl, new()
        {
            return new SettingsTabInfo(name, App.GetIcon(icon), () => new TTab
            {
                DataContext = ActivatorUtilities.CreateInstance<TTabViewModel>(App.Instance.Services)
            });
        }
    }
}