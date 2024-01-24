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
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Services.OnionDatabase
{
    /// <summary>
    /// Hosted service responsible for managing the onion.db and geoip files
    /// </summary>
    public class OnionDbService(ApiClient client, ILogger<OnionDbService> logger) : IOnionDatabase, IHostedService
    {
        #region Static Values

        // this will need to be moved to a central location when adding in settings, etc.
        private static string DatabasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonFruit Network", "OnionFruit", "onion.db");

        static OnionDbService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
        }

        #endregion

        private Timer _checkTimer;
        private Task _currentCheckTask;
        private CancellationTokenSource _cancellation;

        private OnionDb _currentDb;
        private DatabaseState _state;
        private IReadOnlyCollection<TorNodeCountry> _countries;

        public DatabaseState State
        {
            get => _state;
            private set
            {
                if (_state == value) return;

                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }

        public IReadOnlyCollection<TorNodeCountry> Countries
        {
            get => _countries;
            private set
            {
                _countries = value;
                CountriesChanged?.Invoke(this, value);
            }
        }

        public Task<IReadOnlyDictionary<AddressFamily, FileInfo>> GeoIPFiles { get; private set; }

        public event EventHandler<DatabaseState> StateChanged;
        public event EventHandler<IReadOnlyCollection<TorNodeCountry>> CountriesChanged;

        private void TimerPinged()
        {
            if (_currentDb == null)
            {
                LoadLocalDatabase();
            }

            _currentCheckTask = CheckOnlineUpdates();

            // schedule the timer to check again in 15 seconds if the task fails, or 12 hours if it succeeds
            _currentCheckTask.ContinueWith(_ => _checkTimer.Change(TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan), TaskContinuationOptions.OnlyOnFaulted);
            _currentCheckTask.ContinueWith(_ => _checkTimer.Change(TimeSpan.FromHours(12), Timeout.InfiniteTimeSpan), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private async Task CheckOnlineUpdates()
        {
            // additional guard to prevent checks running once disposed
            if (_cancellation.IsCancellationRequested && _checkTimer != null)
            {
                await _checkTimer.DisposeAsync();
                _checkTimer = null;
            }

            DateTimeOffset? fileLastModified = _currentDb == null ? null : DateTimeOffset.FromUnixTimeSeconds(_currentDb.DbVersion);

            // redownload if file is 0-length or is older than 12 hours
            if (File.Exists(DatabasePath) && fileLastModified - DateTimeOffset.Now < TimeSpan.FromHours(12))
            {
                return;
            }

            using var databaseStream = new FileStream(DatabasePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous);

            State = DatabaseState.Downloading;
            logger.LogInformation("Downloading onion.db (expiry date: {ExpiryDate})...", fileLastModified);

            // todo add progress tracking, handle errors from PerformDownload

            var onionDbRequest = new OnionDbDownloadRequest(fileLastModified);
            await client.PerformDownload(onionDbRequest, databaseStream, null, true, false, _cancellation.Token).ConfigureAwait(false);

            LoadLocalDatabase();
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
                await Task.WhenAll(writers.Values.Select(x => x.WriteHeader(database))).ConfigureAwait(false);

                foreach (var country in database.Countries)
                {
                    cancellation.ThrowIfCancellationRequested();

                    // write ipv4 and ipv6 ranges to the appropriate files
                    if (country.V4Ranges.Count > 0 && writers.TryGetValue(AddressFamily.InterNetwork, out var v4Writer))
                        await v4Writer.WriteRanges(country.CountryCode, country.V4Ranges).ConfigureAwait(false);

                    if (country.V6Ranges.Count > 0 && writers.TryGetValue(AddressFamily.InterNetworkV6, out var v6Writer))
                        await v6Writer.WriteRanges(country.CountryCode, country.V6Ranges).ConfigureAwait(false);
                }

                foreach (var writer in writers.Values)
                {
                    writer.Dispose();
                }
            }
            catch
            {
                // if there is an issue, we should stop and remove the files
                foreach (var writer in writers.Values)
                    writer.Dispose();

                foreach (var file in fileNames.Values)
                    File.Delete(file);

                throw;
            }

            return fileNames.ToDictionary(x => x.Key, x => new FileInfo(x.Value));
        }

        private void LoadLocalDatabase()
        {
            if (!File.Exists(DatabasePath))
            {
                _currentDb = null;
                return;
            }

            State = DatabaseState.Processing;

            using (var localReadStream = new FileStream(DatabasePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
            {
                _currentDb = OnionDb.Parser.ParseFrom(localReadStream);
            }

            Countries = _currentDb.Countries.Select(x => new TorNodeCountry(x.CountryName, x.CountryCode, x.EntryNodeCount, x.ExitNodeCount, x.TotalNodeCount)).ToList();
            GeoIPFiles = WriteGeoIpFiles(_currentDb, _cancellation.Token);

            State = DatabaseState.Ready;
        }

        #region HostedService

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();

            // start timer after loading database
            _checkTimer = new Timer(_ => TimerPinged(), null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);

            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _checkTimer?.Dispose();
            _cancellation?.Cancel();

            return _currentCheckTask?.WaitAsync(cancellationToken) ?? Task.CompletedTask;
        }

        #endregion
    }
}