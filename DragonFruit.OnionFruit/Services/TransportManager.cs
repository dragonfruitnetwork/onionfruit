// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DragonFruit.OnionFruit.Core;
using DragonFruit.OnionFruit.Core.Transports;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Services
{
    public class TransportManager
    {
        public TransportManager(ExecutableLocator locator, ILogger<TransportManager> logger)
        {
            // locate and read pt_config.json
            var ptConfigLocation = locator.LocateExecutableInstancesOf("tor")
                .SelectMany(x => Directory.EnumerateFiles(Path.GetDirectoryName(x), "pt_config.json", SearchOption.AllDirectories))
                .First();

            if (string.IsNullOrEmpty(ptConfigLocation))
            {
                logger.LogWarning("Failed to locate pluggable transports folder. No obfuscated transports will be available");

                AvailableTransports = new Dictionary<TransportType, TransportInfo>();
                RecommendedTransport = null;
                return;
            }

            TransportType? recommendedTransport = null;
            Dictionary<TransportType, TransportInfo> availableTransports = [];

            // load the pt_config.json file (currently sync, could be async?)
            using (var readStream = File.OpenRead(ptConfigLocation))
            {
                Config = JsonSerializer.Deserialize<PluggableTransportConfig>(readStream);
            }

            // get all transports that can be used
            foreach (var transport in Enum.GetValues<TransportType>())
            {
                var transportName = transport.ToString();
                var metadata = typeof(TransportType).GetMember(transportName)[0].GetCustomAttribute<TransportInfo>();

                if (metadata == null)
                {
                    continue;
                }

                if (transportName == Config.RecommendedDefault)
                {
                    recommendedTransport = transport;
                }

                if (string.IsNullOrEmpty(metadata.TransportEngine) || Config.PluggableTransports.ContainsKey(metadata.TransportEngine))
                {
                    availableTransports.Add(transport, metadata);

                    continue;
                }

                logger.LogWarning("Cannot use transport {TransportType} as the required engine {Engine} is not available", transport, metadata.TransportEngine);
            }

            // determine the relative path to the transport directory from the tor executable
            var torExecutableDirectory = Path.GetDirectoryName(locator.LocateExecutableInstancesOf("tor").First());
            var relTransportDirectory = Path.GetRelativePath(torExecutableDirectory, Path.GetDirectoryName(ptConfigLocation)).TrimEnd(Path.DirectorySeparatorChar);

            // set properties
            AvailableTransports = availableTransports.ToFrozenDictionary();
            TransportConfigLines = Config.PluggableTransports.ToFrozenDictionary(x => x.Key, x => x.Value.Replace("${pt_path}", $"{relTransportDirectory}{Path.DirectorySeparatorChar}"));

            if (recommendedTransport.HasValue && !availableTransports.ContainsKey(recommendedTransport.Value))
            {
                RecommendedTransport = null;
            }
            else
            {
                RecommendedTransport = recommendedTransport;
            }
        }

        internal PluggableTransportConfig Config { get; }

        /// <summary>
        /// Gets the recommended transport type
        /// </summary>
        public TransportType? RecommendedTransport { get; }

        /// <summary>
        /// Gets a dictionary of the transport configuration lines for each transport
        /// </summary>
        public IReadOnlyDictionary<string, string> TransportConfigLines { get; }

        /// <summary>
        /// Gets a list of the available transports with support on the current system
        /// </summary>
        public IEnumerable<TransportType> AvailableTransportTypes => AvailableTransports.Keys;

        /// <summary>
        /// Gets a list of the available transports and info about them on the current system
        /// </summary>
        public IReadOnlyDictionary<TransportType, TransportInfo> AvailableTransports { get; }
    }
}