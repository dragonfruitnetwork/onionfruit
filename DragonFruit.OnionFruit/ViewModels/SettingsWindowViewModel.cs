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
    public record SettingsTabInfo(string Id, string Name, IconSource Icon, Func<Control> ContentFactory);

    public class SettingsWindowViewModel : ReactiveObject, IDisposable
    {
        private SettingsTabInfo _selectedTab;

        internal const string AboutTabId = "about";

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

            FooterTabs.Add(IdentifiableTab<AboutPageTabView, AboutPageTabViewModel>(AboutTabId, "About OnionFruit™", LucideIconNames.Info));
        }

        public SettingsTabInfo SelectedTab
        {
            get => _selectedTab ??= Tabs.FirstOrDefault();
            set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
        }

        public IDataTemplate TabTemplate { get; } = new SettingTabViewTemplate();
        public IObservableCollection<SettingsTabInfo> Tabs { get; } = new ObservableCollectionExtended<SettingsTabInfo>();
        public IObservableCollection<SettingsTabInfo> FooterTabs { get; } = new ObservableCollectionExtended<SettingsTabInfo>();

        public void SetActiveTab(string tabId)
        {
            SelectedTab = Tabs.Concat(FooterTabs).FirstOrDefault(x => x.Id == tabId);
        }

        public void Dispose()
        {
            (TabTemplate as IDisposable)?.Dispose();
        }

        protected static SettingsTabInfo Tab<TTab, TTabViewModel>(string name, LucideIconNames icon) where TTab : UserControl, new()
        {
            return IdentifiableTab<TTab, TTabViewModel>(null, name, icon);
        }

        protected static SettingsTabInfo IdentifiableTab<TTab, TTabViewModel>(string id, string name, LucideIconNames icon) where TTab : UserControl, new()
        {
            return new SettingsTabInfo(id, name, App.GetIcon(icon), () => new TTab
            {
                DataContext = ActivatorUtilities.CreateInstance<TTabViewModel>(App.Instance.Services)
            });
        }
    }
}