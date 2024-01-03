// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Exposes filesystem options (cache dir, data dir)
    /// </summary>
    public class FilesystemOptions(string dataDirectory) : TorrcConfigEntry
    {
        /// <summary>
        /// Whether to avoid disk writes
        /// </summary>
        public bool AvoidDiskWrites { get; set; }

        /// <summary>
        /// The directory to store cached data in
        /// </summary>
        public string CacheDirectory { get; set; }

        /// <summary>
        /// The directory to store persisted data in
        /// </summary>
        public string DataDirectory { get; set; } = dataDirectory;

        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (!string.IsNullOrEmpty(DataDirectory) && !Directory.Exists(DataDirectory))
            {
                yield return new ConfigEntryValidationResult(true, "Data directory does not exist.");
            }

            if (!string.IsNullOrEmpty(CacheDirectory) && !Directory.Exists(CacheDirectory))
            {
                yield return new ConfigEntryValidationResult(true, "Cached data directory does not exist.");
            }
        }

        public override async Task WriteAsync(StreamWriter writer)
        {
            await writer.WriteLineAsync($"AvoidDiskWrites {(AvoidDiskWrites ? 1 : 0)}").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(DataDirectory))
            {
                await writer.WriteLineAsync($"DataDirectory {DataDirectory}").ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(CacheDirectory))
            {
                await writer.WriteLineAsync($"CacheDataDir {CacheDirectory}").ConfigureAwait(false);
            }
        }
    }
}