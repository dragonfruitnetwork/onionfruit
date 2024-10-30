// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;

namespace DragonFruit.OnionFruit.Updater
{
    public interface IOnionFruitUpdater
    {
        /// <summary>
        /// The current updater status
        /// </summary>
        public OnionFruitUpdaterStatus Status { get; }

        /// <summary>
        /// Event fired when the updater status changes
        /// </summary>
        event Action<OnionFruitUpdaterStatus> StatusChanged;

        /// <summary>
        /// Triggers a manual check for updates
        /// </summary>
        void TriggerUpdateCheck();
    }
}