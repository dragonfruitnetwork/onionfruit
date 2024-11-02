// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using Avalonia;
using Avalonia.Platform;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.Updater;
using DragonFruit.OnionFruit.ViewModels;
using DragonFruit.OnionFruit.ViewModels.Interfaces;

namespace DragonFruit.OnionFruit.Windows.ViewModels
{
    public class Win32MainWindowViewModel(TorSession session, IOnionDatabase onionDatabase, IOnionFruitUpdater updater, OnionFruitSettingsStore settings)
        : MainWindowViewModel(session, onionDatabase, updater, settings), IHasCustomStartupPosition
    {
        PixelPoint IHasCustomStartupPosition.GetInitialPosition(Screen screen, Size clientSize)
        {
            var screenSize = screen.WorkingArea.Size;
            var scaling = screen.Scaling;

            var padding = (int)(10 * scaling);
            var windowSize = PixelSize.FromSize(clientSize, scaling);

            var scaledOffsetX = (int)Math.Ceiling((screen.Bounds.Width - screenSize.Width) * (1 / scaling));
            var scaledOffsetY = (int)Math.Ceiling((screen.Bounds.Height - screenSize.Height) * (1 / scaling));

            var xPosition = screenSize.Width - windowSize.Width - (padding - scaledOffsetX);
            var yPosition = screenSize.Height - windowSize.Height - (padding - scaledOffsetY);

            // handle taskbar position
            xPosition -= (screen.Bounds.Width - screen.WorkingArea.Width);
            yPosition -= (screen.Bounds.Height - screen.WorkingArea.Height);

            return new PixelPoint(xPosition, yPosition);
        }
    }
}