// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Describes a validation warning or error
    /// </summary>
    /// <param name="IsError">Whether the message represents an error</param>
    /// <param name="Message">The message content</param>
    public record ConfigEntryValidationResult(bool IsError, string Message);

    /// <summary>
    /// Represents a configuration entry that can be written to a torrc file.
    /// </summary>
    public abstract class TorrcConfigEntry
    {
        /// <summary>
        /// Performs a validation check on the setting, returning a list of <see cref="ConfigEntryValidationResult"/>s if any errors are found.
        /// </summary>
        /// <returns>An empty <see cref="IEnumerable{T}"/> if no errors are found, otherwise a collection of validation warnings/errors</returns>
        public virtual IEnumerable<ConfigEntryValidationResult> PerformValidation() => Enumerable.Empty<ConfigEntryValidationResult>();

        /// <summary>
        /// Asynchronously writes the setting to the provided <see cref="StreamWriter"/>
        /// </summary>
        public abstract Task WriteAsync(StreamWriter writer);
    }
}