// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Represents a configuration category that allows for custom entries to be added.
    /// This should be used with caution, as it can break the configuration if used incorrectly
    /// </summary>
    public class CustomConfig : TorrcConfigEntry
    {
        public IEnumerable<string> Lines { get; set; }

        public override async Task WriteAsync(StreamWriter writer)
        {
            foreach (var line in Lines)
            {
                await writer.WriteLineAsync(line);
            }
        }
    }
}