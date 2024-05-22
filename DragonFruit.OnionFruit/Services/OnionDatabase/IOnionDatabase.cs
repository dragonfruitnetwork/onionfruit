// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Models;

namespace DragonFruit.OnionFruit.Services.OnionDatabase
{
    public interface IOnionDatabase
    {
        internal const string TorCountryCode = "T1";

        /// <summary>
        /// The current state of the database
        /// </summary>
        DatabaseState State { get; }

        /// <summary>
        /// List of countries with Tor nodes
        /// </summary>
        IReadOnlyCollection<TorNodeCountry> Countries { get; }

        /// <summary>
        /// Collection of GeoIP files. If the files don't currently exist, the task will be completed once they are available
        /// </summary>
        Task<IReadOnlyDictionary<AddressFamily, FileInfo>> GeoIPFiles { get; }

        /// <summary>
        /// Event invoked when the database state changes
        /// </summary>
        event EventHandler<DatabaseState> StateChanged;

        /// <summary>
        /// Event invoked when the countries list is updated
        /// </summary>
        event EventHandler<IReadOnlyCollection<TorNodeCountry>> CountriesChanged;
    }
}