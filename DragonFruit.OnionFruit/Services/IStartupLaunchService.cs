// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Services
{
    public interface IStartupLaunchService
    {
        /// <summary>
        /// Gets whether the app can be enabled to start on boot (more specifically, user login)
        /// </summary>
        StartupLaunchState CurrentStartupState { get; }

        /// <summary>
        /// Gets whether the current instance was launched by the startup service
        /// </summary>
        bool InstanceLaunchedByStartupService { get; }

        /// <summary>
        /// Sets whether the app should start on boot, returning the newly set state
        /// </summary>
        StartupLaunchState SetStartupState(bool enabled);
    }

    public enum StartupLaunchState
    {
        /// <summary>
        /// Cannot be enabled to start on boot, either due to portable install or missing permissions
        /// </summary>
        Blocked,

        /// <summary>
        /// The app is not set to start on boot
        /// </summary>
        Disabled,

        /// <summary>
        /// The app is set to start on boot
        /// </summary>
        Enabled,

        /// <summary>
        /// The app is set to start on boot, but there is a configuration error that could prevent it from working as expected
        /// </summary>
        EnabledInvalid
    }
}