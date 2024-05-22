// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using Avalonia;
using Avalonia.Platform;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.ViewModels.Interfaces;

namespace DragonFruit.OnionFruit.Windows.ViewModels
{
    public class Win32MainWindowViewModel(TorSession session, IOnionDatabase onionDatabase) : MainWindowViewModel(session, onionDatabase), IHasCustomStartupPosition
    {
        PixelPoint IHasCustomStartupPosition.GetInitialPosition(Screen screen, Size clientSize)
        {
            var screenSize = screen.WorkingArea.Size;

            var padding = (int)(10 * screen.Scaling);
            var windowSize = PixelSize.FromSize(clientSize, screen.Scaling);

            // account for when scaling != 1: the taskbar is scaled, but the workingarea doesn't account for that.
            var scaledOffsetX = (int)Math.Ceiling((screen.Bounds.Width - screenSize.Width) * (1 / screen.Scaling));
            var scaledOffsetY = (int)Math.Ceiling((screen.Bounds.Height - screenSize.Height) * (1 / screen.Scaling));

            // apply padding to x axis only - the title bar is merged with the window so it gets its own padding
            return new PixelPoint(screenSize.Width - windowSize.Width - scaledOffsetX - padding, screenSize.Height - windowSize.Height - scaledOffsetY + padding);
        }
    }
}