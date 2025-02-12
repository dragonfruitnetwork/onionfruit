// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Services
{
    public interface IProcessElevator
    {
        /// <summary>
        /// Checks whether the current process can be elevated
        /// </summary>
        bool CheckElevationStatus();

        /// <summary>
        /// Attempts to increase the permissions of the current process, returning whether the operation was successful
        /// </summary>
        bool ElevatePermissions();
    }
}