// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Provides options for selecting nodes to use in a circuit, along with the ability to specify the GeoIP database files to use
    /// </summary>
    public class NodeSelectionOptions : TorrcConfigEntry
    {
        private string _geoIPv4File, _geoIPv6File;

        /// <summary>
        /// Path to the GeoIP database file
        /// </summary>
        public string GeoIPv4File
        {
            get => _geoIPv4File;
            set => _geoIPv4File = Path.GetFullPath(value);
        }

        /// <summary>
        /// Path to the GeoIPv6 database file
        /// </summary>
        public string GeoIPv6File
        {
            get => _geoIPv6File;
            set => _geoIPv6File = Path.GetFullPath(value);
        }

        /// <summary>
        /// Whether to only use nodes that are specified in the <see cref="INodeFilter"/> collections.
        /// Setting this to true may result in circuits being unable to be built.
        /// </summary>
        public bool StrictNodes { get; set; }

        /// <summary>
        /// Whether to exclude nodes that cannot be geolocated (country code {??}).
        /// Defaults based on whether country codes have been provided in entry/exit selection criteria.
        /// </summary>
        public bool? ExcludeUnknownNodes { get; set; }

        /// <summary>
        /// Collection of <see cref="INodeFilter"/>s used to identify suitable exit nodes to use
        /// </summary>
        public ICollection<INodeFilter> ExitNodes { get; set; }

        /// <summary>
        /// Collection of <see cref="INodeFilter"/>s used to identify suitable entry nodes to use
        /// </summary>
        public ICollection<INodeFilter> EntryNodes { get; set; }

        /// <summary>
        /// <see cref="INodeFilter"/>s used to select nodes that should be used as middleman nodes
        /// </summary>
        /// <remarks>
        /// This is an experimental feature that is meant to be used by researchers and developers to test new features in the Tor network safely.
        /// </remarks>
        public ICollection<INodeFilter> MiddleNodes { get; set; }

        /// <summary>
        /// <see cref="INodeFilter"/>s used to identify nodes that should be excluded from selection
        /// </summary>
        public ICollection<INodeFilter> ExcludedNodes { get; set; }

        /// <summary>
        /// <see cref="INodeFilter"/>s used to identify nodes that should be excluded from exit node selection
        /// </summary>
        /// <remarks>
        /// All identifiers in <see cref="ExcludedNodes"/> are implicitly excluded from exit node selection
        /// </remarks>
        public ICollection<INodeFilter> ExcludedExitNodes { get; set; }

        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (!string.IsNullOrWhiteSpace(GeoIPv4File) && !File.Exists(GeoIPv4File))
            {
                yield return new ConfigEntryValidationResult(true, "GeoIPv4 file does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(GeoIPv6File) && !File.Exists(GeoIPv6File))
            {
                yield return new ConfigEntryValidationResult(true, "GeoIPv6 file does not exist.");
            }
        }

        public override async Task WriteAsync(StreamWriter writer)
        {
            await writer.WriteLineAsync($"StrictNodes {(StrictNodes ? 1 : 0)}").ConfigureAwait(false);

            await WriteNodeFiltersAsync(writer, "ExcludeExitNodes", ExcludedExitNodes).ConfigureAwait(false);
            await WriteNodeFiltersAsync(writer, "ExcludeNodes", ExcludedNodes).ConfigureAwait(false);
            await WriteNodeFiltersAsync(writer, "MiddleNodes", MiddleNodes).ConfigureAwait(false);
            await WriteNodeFiltersAsync(writer, "EntryNodes", EntryNodes).ConfigureAwait(false);
            await WriteNodeFiltersAsync(writer, "ExitNodes", ExitNodes).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(GeoIPv4File) && File.Exists(GeoIPv4File))
            {
                await writer.WriteLineAsync($"GeoIPv4File {GeoIPv4File}").ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(GeoIPv6File) && File.Exists(GeoIPv6File))
            {
                await writer.WriteLineAsync($"GeoIPv6File {GeoIPv6File}").ConfigureAwait(false);
            }

            if (ExcludeUnknownNodes.HasValue)
            {
                await writer.WriteLineAsync($"ExcludeUnknownNodes {(ExcludeUnknownNodes.Value ? 1 : 0)}").ConfigureAwait(false);
            }
        }

        private async Task WriteNodeFiltersAsync(StreamWriter writer, string identifier, ICollection<INodeFilter> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return;
            }

            var identifierBuilder = new StringBuilder($"{identifier} ");

            foreach (var filter in filters)
            {
                identifierBuilder.Append(filter.TorrcIdentifier);
                identifierBuilder.Append(',');
            }

            identifierBuilder.Length--; // remove trailing comma
            await writer.WriteLineAsync(identifierBuilder).ConfigureAwait(false);
        }
    }

    public interface INodeFilter
    {
        /// <summary>
        /// Gets the identifier for the node that would be used in a torrc file
        /// </summary>
        string TorrcIdentifier { get; }
    }

    /// <summary>
    /// Filters a single node based on its fingerprint
    /// </summary>
    /// <param name="Fingerprint">The 40-character hexadecimal node fingerprint</param>
    public partial record NodeFingerprintFilter(string Fingerprint) : INodeFilter
    {
        string INodeFilter.TorrcIdentifier => Fingerprint;

        public bool IsValid => FingerprintRegex().IsMatch(Fingerprint);

        // valid fingerprints are 40 characters long and only contain hex characters (e.g. ABCD1234CDEF5678ABCD1234CDEF5678ABCD1234)
        [GeneratedRegex("^[A-F0-9]{40}$", RegexOptions.IgnoreCase, "en-US")]
        private partial Regex FingerprintRegex();
    }

    /// <summary>
    /// Filters nodes based on their country code
    /// </summary>
    /// <param name="CountryCode">ISO3166 country code (case insensitive)</param>
    public record NodeCountryFilter(string CountryCode) : INodeFilter
    {
        string INodeFilter.TorrcIdentifier => $"{{{CountryCode.ToLowerInvariant()}}}";
    }

    /// <summary>
    /// Filters nodes based on their <see cref="IPAddress"/>
    /// </summary>
    /// <param name="AddressRange">The <see cref="IPNetwork"/> to filter by</param>
    public record NodeAddressRangeFilter(IPNetwork AddressRange) : INodeFilter
    {
        // todo this doesn't print ipv6 addresses with square brackets (check if needed)
        string INodeFilter.TorrcIdentifier => AddressRange.ToString();
    }
}