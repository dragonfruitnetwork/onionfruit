// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Text.RegularExpressions;

namespace DragonFruit.OnionFruit.Core
{
    /// <summary>
    /// Provides regex patterns for matching against Tor process outputs
    /// </summary>
    public static partial class TorProcessOutputRegex
    {
        /// <summary>
        /// Regex used for matching the version from the `tor --version` command
        /// </summary>
        [GeneratedRegex(@"Tor version (.+?)\.?$", RegexOptions.Multiline, "en-US")]
        public static partial Regex VersionOutput();

        /// <summary>
        /// Generic console log message pattern. Used when the tor process is executed normally
        /// </summary>
        [GeneratedRegex(@"(?<date>\w{3,} \d{1,2} [\d:.]+) \[(?<level>.+)\] (?<message>.+)", RegexOptions.IgnoreCase, "en-US")]
        public static partial Regex ConsoleLogOutput();

        /// <summary>
        /// Bootstrap message from generic log output. Can be used with the capture group `message` from <see cref="ConsoleLogOutput"/> or a raw line
        /// </summary>
        [GeneratedRegex(@"Bootstrapped (?<progress>\d{1,3})% \((?<state>\w+)\): (?<message>.+)", RegexOptions.IgnoreCase, "en-US")]
        public static partial Regex BootstrapLogOutput();
    }
}