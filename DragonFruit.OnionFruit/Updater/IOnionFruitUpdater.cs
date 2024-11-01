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
        public OnionFruitUpdaterStatus Status { get; }

        /// <summary>
        /// The current download progress
        /// </summary>
        public int? DownloadProgress { get; }

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

        /// <summary>
        /// Method run on application exit. Can be used to trigger the full update.
        /// </summary>
        Task AppExitCallback(bool restart);
    }
}