// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Data;
using DragonFruit.OnionFruit.Models;
using Microsoft.Extensions.Hosting;

namespace DragonFruit.OnionFruit.Services.OnionDatabase
{
    /// <summary>
    /// Hosted service responsible for managing the onion.db and geoip files
    /// </summary>
    public class OnionDbService(ApiClient client) : IHostedService, IOnionDatabase
    {
        // this will need to be moved to a central location when adding in settings, etc.
        private static string DatabasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonFruit Network", "OnionFruit", "onion.db");

        static OnionDbService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
        }

        private Timer _checkTimer;
        private Task _currentCheckTask;
        private CancellationTokenSource _cancellation;

        private IReadOnlyCollection<TorNodeCountry> _countries;

        /// <inheritdoc/>
        public event EventHandler CountriesUpdated;

        /// <inheritdoc/>
        public IReadOnlyCollection<TorNodeCountry> Countries
        {
            get => _countries;
            set
            {
                _countries = value;
                CountriesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        public Task<IReadOnlyDictionary<AddressFamily, FileInfo>> GeoIpFiles { get; private set; }

        private void TimerPinged()
        {
            _currentCheckTask = CheckDatabase();

            // schedule the timer to check again in 15 seconds if the task fails, or 12 hours if it succeeds
            _currentCheckTask.ContinueWith(_ => _checkTimer.Change(TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan), TaskContinuationOptions.OnlyOnFaulted);
            _currentCheckTask.ContinueWith(_ => _checkTimer.Change(TimeSpan.FromHours(12), Timeout.InfiniteTimeSpan), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private async Task CheckDatabase()
        {
            OnionDb currentDb;
            DateTimeOffset? fileLastModified = null;

            // check for an onion.db file
            if (File.Exists(DatabasePath))
            {
                fileLastModified = File.GetLastWriteTimeUtc(DatabasePath);
            }

            using (var databaseStream = new FileStream(DatabasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                // redownload if file has 0-length or is older than 12 hours
                if (databaseStream.Length == 0 || DateTimeOffset.UtcNow - fileLastModified > TimeSpan.FromHours(12))
                {
                    // todo add progress tracking, handle errors from PerformDownload

                    var onionDbRequest = new OnionDbDownloadRequest(fileLastModified);
                    await client.PerformDownload(onionDbRequest, databaseStream, null, true, false, _cancellation.Token).ConfigureAwait(false);
                }

                databaseStream.Seek(0, SeekOrigin.Begin);
                currentDb = OnionDb.Parser.ParseFrom(databaseStream);
            }

            Countries = currentDb.Countries.Select(x => new TorNodeCountry(x.CountryName, x.CountryCode, x.EntryNodeCount, x.ExitNodeCount, x.TotalNodeCount)).ToList();
            GeoIpFiles = WriteGeoIpFiles(currentDb, _cancellation.Token);
        }

        /// <summary>
        /// Checks for GeoIP files in the expected location and writes a new set if they don't exist
        /// </summary>
        private static async Task<IReadOnlyDictionary<AddressFamily, FileInfo>> WriteGeoIpFiles(OnionDb database, CancellationToken cancellation = default)
        {
            var fileNames = new Dictionary<AddressFamily, string>
            {
                [AddressFamily.InterNetwork] = Path.Combine(Path.GetTempPath(), $"oniondb-{database.DbVersion}.geoip"),
                [AddressFamily.InterNetworkV6] = Path.Combine(Path.GetTempPath(), $"oniondb-{database.DbVersion}.geoip6")
            };

            if (fileNames.Values.All(File.Exists))
            {
                return fileNames.ToDictionary(x => x.Key, x => new FileInfo(x.Value));
            }

            var writers = fileNames.ToDictionary(x => x.Key, x => new GeoIpWriter(x.Value));

            try
            {
                // write headers to all files
                await Task.WhenAll(writers.Values.Select(x => x.WriteHeader(database))).ConfigureAwait(false);

                foreach (var country in database.Countries)
                {
                    cancellation.ThrowIfCancellationRequested();

                    // write ipv4 and ipv6 ranges to the appropriate files
                    if (country.V4Ranges.Count > 0 && writers.TryGetValue(AddressFamily.InterNetwork, out var v4Writer))
                        await v4Writer.WriteRanges(country.CountryCode, country.V4Ranges).ConfigureAwait(false);

                    if (country.V6Ranges.Count > 0 && writers.TryGetValue(AddressFamily.InterNetworkV6, out var v6Writer))
                        await v6Writer.WriteRanges(country.CountryCode, country.V4Ranges).ConfigureAwait(false);
                }

                foreach (var writer in writers.Values)
                    writer.Dispose();
            }
            catch
            {
                // if there is an issue, we should stop and remove the files
                foreach (var writer in writers.Values)
                    writer.Dispose();

                foreach (var file in fileNames.Values)
                    File.Delete(file);
            }

            return fileNames.ToDictionary(x => x.Key, x => new FileInfo(x.Value));
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _cancellation?.Dispose();

            _cancellation = new CancellationTokenSource();
            _checkTimer = new Timer(_ => TimerPinged(), null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);

            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _checkTimer?.Dispose();
            _cancellation?.Cancel();

            return _currentCheckTask ?? Task.CompletedTask;
        }
    }
}