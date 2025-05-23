// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Services
{
    public interface IProcessElevator
    {
        /// <summary>
        /// Checks whether the current process can be elevated
        /// </summary>
        ElevationStatus CheckElevationStatus();

        /// <summary>
        /// Attempts to increase the permissions of the current process, returning whether the operation was successful
        /// </summary>
        bool RelaunchProcess(bool elevated);
    }

    public enum ElevationStatus
    {
        /// <summary>
        /// Process is already elevated
        /// </summary>
        Elevated,

        /// <summary>
        /// Process can be elevated
        /// </summary>
        CanElevate,

        /// <summary>
        /// Process cannot be elevated
        /// </summary>
        CannotElevate
    }
}