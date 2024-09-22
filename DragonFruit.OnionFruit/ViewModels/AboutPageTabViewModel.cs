// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DragonFruit.OnionFruit.Models;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class AboutPageTabViewModel : ReactiveObject
    {
        public AboutPageTabViewModel()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream("DragonFruit.OnionFruit.Assets.nuget-licenses.json");
            using var readStream = new StreamReader(stream);

            Packages = JsonSerializer.Deserialize<IEnumerable<NugetPackageLicenseInfo>>(readStream.ReadToEnd());
        }

        public IEnumerable<NugetPackageLicenseInfo> Packages { get; }

        public IconSource UpdaterIcon => App.GetIcon(LucideIconNames.RefreshCw);
        public IconSource LicensesIcon => App.GetIcon(LucideIconNames.Scale);
    }
}