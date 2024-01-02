// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.IO;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Represents the `AvoidDiskWrites` option in the torrc file
    /// </summary>
    /// <remarks>
    /// If <c>true</c>, tor will try to write to disk less frequently than normally. This is useful when running on flash memory or other media that support only a limited number of writes.
    /// </remarks>
    public class AvoidDiskWritesOption(bool avoidDiskWrites) : TorrcConfigEntry
    {
        /// <summary>
        /// Whether to avoid disk writes
        /// </summary>
        public bool AvoidDiskWrites { get; set; } = avoidDiskWrites;

        public override Task WriteAsync(StreamWriter writer)
        {
            return writer.WriteLineAsync($"AvoidDiskWrites {(AvoidDiskWrites ? 1 : 0)}");
        }
    }
}