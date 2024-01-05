// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DragonFruit.OnionFruit.Core
{
    /// <summary>
    /// Allows for locating an executable on the current system via normal search paths
    /// </summary>
    public abstract class ExecutableLocator
    {
        /// <summary>
        /// Gets the suffix applied to the executable (i.e. ".exe" on Windows, <see cref="string.Empty"/> on macOS/Linux)
        /// </summary>
        public abstract string ExecutableSuffix { get; }

        /// <summary>
        /// A list of platform identifiers that a runnable executable could be found under
        /// </summary>
        public abstract IEnumerable<string> SupportedPlatforms { get; }

        public virtual IEnumerable<string> LocateExecutableInstancesOf(string executableName)
        {
            return GeneratePotentialCandidates().Select(path => Path.Combine(path, executableName + ExecutableSuffix)).Where(File.Exists);
        }

        private IEnumerable<string> GeneratePotentialCandidates()
        {
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // no reason for the second check to fail but just in case...
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                // return base dir
                yield return root;

                // check native win/osx/linux-* dirs
                foreach (var rid in SupportedPlatforms)
                {
                    yield return Path.Combine(root, "runtimes", rid, "native");
                }
            }

            // %SystemRoot%/system32
            yield return Environment.GetFolderPath(Environment.SpecialFolder.System);

            if (OperatingSystem.IsWindows())
            {
                // %SystemRoot%
                yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            }

            // run through all PATH locations (user, machine, etc.)
            foreach (var path in Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>())
            {
                yield return path;
            }
        }
    }
}