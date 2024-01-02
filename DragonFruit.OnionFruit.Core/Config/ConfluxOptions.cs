// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Represents the `ConfluxEnabled` and `ConfluxClientUX` options in the torrc file
    /// </summary>
    public class ConfluxOptions(bool? enabled, ConfluxOptions.ClientUx? experienceType) : TorrcConfigEntry
    {
        /// <summary>
        /// Gets or sets whether the conflux traffic splitting.
        /// If <c>null</c>, the conflux will be automatically enabled based on the consensus.
        /// </summary>
        public bool? Enabled { get; set; } = enabled;

        /// <summary>
        /// The mode the conflux should operate in.
        /// </summary>
        public ClientUx? ExperienceType { get; set; } = experienceType;

        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (Enabled == null && ExperienceType == null)
            {
                yield return new ConfigEntryValidationResult(false, "No conflux settings were specified. Defaults will be used.");
            }
        }

        public override async Task WriteAsync(StreamWriter writer)
        {
            var enabledKeyword = Enabled switch
            {
                true => "1",
                false => "0",

                _ => "auto"
            };

            await writer.WriteLineAsync($"ConfluxEnabled {enabledKeyword}").ConfigureAwait(false);

            if (ExperienceType.HasValue)
            {
                var keyword = ExperienceType.Value switch
                {
                    ClientUx.Throughput => "throughput",
                    ClientUx.Latency => "latency",
                    ClientUx.ThroughputLowMemory => "throughput_lowmem",
                    ClientUx.LatencyLowMemory => "latency_lowmem",

                    _ => throw new ArgumentOutOfRangeException()
                };

                await writer.WriteLineAsync($"ConfluxClientUX {keyword}").ConfigureAwait(false);
            }
        }

        public enum ClientUx
        {
            /// <summary>
            /// Maximise traffic throughput.
            /// </summary>
            Throughput,

            /// <summary>
            /// Use the circuit with the lowest latency to process all packets.
            /// </summary>
            Latency,

            /// <summary>
            /// Aim to maximize throughput while using as little memory as possible.
            /// </summary>
            ThroughputLowMemory,

            /// <summary>
            /// Aim to minimise latency while using as little memory as possible.
            /// </summary>
            LatencyLowMemory
        }
    }
}