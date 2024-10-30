// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Updater
{
    public enum OnionFruitUpdaterStatus
    {
        /// <summary>
        /// Checking for updates
        /// </summary>
        Checking,

        /// <summary>
        /// Downloading update files
        /// </summary>
        Downloading,

        /// <summary>
        /// Updates downloaded, currently extracting and applying
        /// </summary>
        Applying,

        /// <summary>
        /// Updates downloaded, pending restart to install
        /// </summary>
        PendingRestart,

        /// <summary>
        /// Update failed
        /// </summary>
        Failed,

        /// <summary>
        /// No updates to apply
        /// </summary>
        UpToDate
    }
}