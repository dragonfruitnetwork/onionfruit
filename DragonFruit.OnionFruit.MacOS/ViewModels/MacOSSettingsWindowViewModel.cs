// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using DragonFruit.OnionFruit.MacOS.Views;
using DragonFruit.OnionFruit.ViewModels;
using LucideAvalonia.Enum;

namespace DragonFruit.OnionFruit.MacOS.ViewModels
{
    public class MacOSSettingsWindowViewModel : SettingsWindowViewModel
    {
        public MacOSSettingsWindowViewModel()
        {
            FooterTabs.Insert(0, Tab<ServiceManagementTabView, ServiceManagementTabViewModel>("Service Management", LucideIconNames.Cog));
        }
    }
}