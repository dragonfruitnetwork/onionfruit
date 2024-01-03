// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Options for configuring dormant mode options
    /// </summary>
    public class DormantModeOptions : TorrcConfigEntry
    {
        /// <summary>
        /// How long the process should go with no user activity before entering dormant mode.
        /// Setting to <c>null</c> disables the timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Whether the process will always launch in active mode.
        /// </summary>
        public bool AlwaysLaunchActiveMode { get; set; }

        /// <summary>
        /// On the first startup, whether the process will enter dormant mode.
        /// Recommended for use cases that install the tor client but can't guarantee it will be used.
        /// </summary>
        public bool DormantOnFirstStartup { get; set; }

        /// <summary>
        /// Whether the process will not dormant mode when idle streams are active.
        /// If <c>false</c>, only network activity counts as user activity.
        /// </summary>
        public bool TimeoutDisabledByIdleStreams { get; set; } = true;


        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (Timeout.HasValue && Timeout < TimeSpan.FromMinutes(10))
            {
                yield return new ConfigEntryValidationResult(true, $"{nameof(Timeout)} must be at least 10 minutes");
            }
        }

        public override async Task WriteAsync(StreamWriter writer)
        {
            if (Timeout.HasValue && Timeout.Value > TimeSpan.Zero)
            {
                await writer.WriteLineAsync("DormantTimeoutEnabled 1").ConfigureAwait(false);
                await writer.WriteLineAsync($"DormantClientTimeout {(int)Timeout.Value.TotalMinutes} minutes").ConfigureAwait(false);
            }
            else
            {
                await writer.WriteLineAsync("DormantTimeoutEnabled 0").ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"DormantOnFirstStartup {(DormantOnFirstStartup ? 1 : 0)}").ConfigureAwait(false);
            await writer.WriteLineAsync($"DormantCanceledByStartup {(AlwaysLaunchActiveMode ? 1 : 0)}").ConfigureAwait(false);
            await writer.WriteLineAsync($"DormantTimeoutDisabledByIdleStreams {(TimeoutDisabledByIdleStreams ? 1 : 0)}").ConfigureAwait(false);
        }
    }
}