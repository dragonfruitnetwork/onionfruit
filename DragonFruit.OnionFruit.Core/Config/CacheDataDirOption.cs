// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Represents the `CacheDataDir` option in the torrc file
    /// </summary>
    public class CacheDataDirOption(string dataDirectory) : TorrcConfigEntry
    {
        /// <summary>
        /// The directory to store cached data in
        /// </summary>
        public string DataDirectory { get; set; } = dataDirectory;

        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (!Directory.Exists(DataDirectory))
            {
                yield return new ConfigEntryValidationResult(true, $"Directory {DataDirectory} does not exist.");
            }
        }

        public override Task WriteAsync(StreamWriter writer)
        {
            return writer.WriteLineAsync($"CacheDataDir {DataDirectory}");
        }
    }
}