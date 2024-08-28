// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using DragonFruit.OnionFruit.Database;

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
    public record TorNodeCountry(string CountryName, string CountryCode, uint EntryNodeCount, uint ExitNodeCount, uint TotalNodeCount)
    {
        public static readonly TorNodeCountry Random = new("Random", IOnionDatabase.TorCountryCode, 0, 0, 0);

        public class TorNodeCountryNameComparer : IComparer<TorNodeCountry>
        {
            public static readonly IComparer<TorNodeCountry> Instance = new TorNodeCountryNameComparer();

            public int Compare(TorNodeCountry x, TorNodeCountry y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (y is null) return 1;
                if (x is null) return -1;

                return string.Compare(x.CountryName, y.CountryName, StringComparison.Ordinal);
            }
        }
    }
}