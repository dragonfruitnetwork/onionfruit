// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using FluentAvalonia.UI.Windowing;

namespace DragonFruit.OnionFruit.Views
{
    public partial class SettingsWindow : AppWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // set titlebar options
            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        }
    }
}