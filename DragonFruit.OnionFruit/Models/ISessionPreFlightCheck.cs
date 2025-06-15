// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Models
{
    public record PreflightCheckFailure(string Message, string? SettingsTabId = null);

    /// <summary>
    /// Exposes methods used to perform pre-flight checks to detect any system misconfiguration issues that may prevent the session from starting.
    /// </summary>
    public interface ISessionPreFlightCheck
    {
        /// <summary>
        /// Perform a pre-flight check, returning an error message and optional settings tab to open if the check fails.
        /// Null is returned if there are no issues.
        /// </summary>
        public PreflightCheckFailure PerformPreFlightCheck();
    }
}