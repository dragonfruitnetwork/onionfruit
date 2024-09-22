// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class AboutPageTabViewModel : ReactiveObject
    {
        public IconSource UpdaterIcon => App.GetIcon(LucideIconNames.RefreshCw);
        public IconSource LicensesIcon => App.GetIcon(LucideIconNames.Scale);
    }
}