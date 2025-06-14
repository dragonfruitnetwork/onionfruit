// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using Avalonia;
using Avalonia.Styling;

namespace DragonFruit.OnionFruit
{
    /// <summary>
    /// Helper class used to selectively apply fixes to FluentAvalonia styles on non-Windows systems.
    /// </summary>
    public class NonWindowsFluentAvaloniaAdjustments : Styles
    {
        public NonWindowsFluentAvaloniaAdjustments()
        {
            if (OperatingSystem.IsWindows())
            {
                Resources["SettingsTabHeaderMargin"] = new Thickness(0);
                return;
            }

            Resources["SettingsTabHeaderMargin"] = new Thickness(0, 15, 0, 0);

            Resources["ButtonPadding"] = new Thickness(12, 15, 12, 8);
            Resources["CheckBoxPadding"] = new Thickness(8, 11, 0, 8);
            Resources["ComboBoxPadding"] = new Thickness(12, 10, 0, 8);
            Resources["TextControlThemePadding"] = new Thickness(10, 8, 6, 8);
            Resources["ComboBoxItemThemePadding"] = new Thickness(11, 10, 11, 8);
        }
    }
}