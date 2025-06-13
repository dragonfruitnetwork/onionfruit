// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Models
{
    /// <summary>
    /// Exposes methods used to perform pre-flight checks to detect any system misconfiguration issues that may prevent the session from starting.
    /// </summary>
    public interface ISessionPreFlightCheck
    {
        /// <summary>
        /// Perform a pre-flight check, returning the identifier of a settings tab that should be opened if the check fails.
        /// Null represents a successful check with no issues found.
        /// </summary>
        public string PerformPreFlightCheck();
    }
}