// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Models
{
    /// <summary>
    /// Represents information about a country's Tor nodes
    /// </summary>
    /// <param name="CountryName">The country name</param>
    /// <param name="CountryCode">The country code (2-letter ISO)</param>
    /// <param name="EntryNodeCount">The number of entry nodes</param>
    /// <param name="ExitNodeCount">The number of exit nodes</param>
    /// <param name="TotalNodeCount">Total count of all nodes in the country</param>
    public record TorNodeCountry(string CountryName, string CountryCode, uint EntryNodeCount, uint ExitNodeCount, uint TotalNodeCount);
}