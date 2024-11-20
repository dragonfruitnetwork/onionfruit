// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Updater
{
    public interface IOnionFruitUpdater
    {
        /// <summary>
        /// The current updater status
        /// </summary>
        OnionFruitUpdaterStatus Status { get; }

        /// <summary>
        /// Whether the application is installed
        /// </summary>
        bool IsInstalled { get; }

        /// <summary>
        /// The current download progress
        /// </summary>
        int? DownloadProgress { get; }

        /// <summary>
        /// Event fired when the updater status changes
        /// </summary>
        event EventHandler<OnionFruitUpdaterStatus> StatusChanged;

        /// <summary>
        /// Event fired when the download progress changes
        /// </summary>
        event EventHandler<int?> DownloadProgressChanged;

        /// <summary>
        /// Triggers a manual check for updates
        /// </summary>
        /// <returns>
        /// A task that completes once the update check has been completed
        /// </returns>
        Task TriggerUpdateCheck();
    }
}