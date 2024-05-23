// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Data;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace DragonFruit.OnionFruit.Database
{
    /// <summary>
    /// Hosted service responsible for managing the onion.db and geoip files
    /// </summary>
    public class OnionDbService : IOnionDatabase, IHostedService, IDisposable
    {
        private const string GeoIpFileTemplate = "oniondb-{0}.geoip{1}";
        private readonly string _databasePath = Path.Combine(App.StoragePath, "onion.db");

        private Timer _checkTimer;
        private Task _currentCheckTask;
        private CancellationTokenSource _cancellation;

        private OnionDb _currentDb;
        private DatabaseState _state;
        private IReadOnlyCollection<TorNodeCountry> _countries;

        private readonly ApiClient _client;
        private readonly ILogger<OnionDbService> _logger;
        private readonly OnionFruitSettingsStore _settings;
        private readonly CompositeDisposable _settingWatchers = new();

        public OnionDbService(ApiClient client, OnionFruitSettingsStore settings, ILogger<OnionDbService> logger)
        {
            _client = client;
            _logger = logger;
            _settings = settings;

            // create ready state observable, run config checks when database state changes or country-related settings are updated
            var databaseReadyObservable = Observable.FromEventPattern<EventHandler<DatabaseState>, DatabaseState>(h => StateChanged += h, h => StateChanged -= h)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(x => x.EventArgs == DatabaseState.Ready);

            foreach (var key in new[] {OnionFruitSetting.TorEntryCountryCode, OnionFruitSetting.TorExitCountryCode})
            {
                settings.GetObservableValue<string>(key)
                    .CombineLatest(databaseReadyObservable)
                    .Where(x => x.Second)
                    .Subscribe(v => ValidateCountrySelection(key, v.First))
                    .DisposeWith(_settingWatchers);
            }
        }

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


        /// <summary>
        /// Checks for GeoIP files in the expected location and writes a new set if they don't exist.
        /// Additionally, starts a file watcher that will automatically rewrite the files should they be edited or deleted.
        /// </summary>
        private async Task<IReadOnlyDictionary<AddressFamily, FileInfo>> PrepareGeoIpFiles(OnionDb database, CancellationToken cancellation = default)
        {
            var fileNames = new Dictionary<AddressFamily, string>
            {
                [AddressFamily.InterNetwork] = Path.Combine(Path.GetTempPath(), string.Format(GeoIpFileTemplate, database.DbVersion, string.Empty)),
                [AddressFamily.InterNetworkV6] = Path.Combine(Path.GetTempPath(), string.Format(GeoIpFileTemplate, database.DbVersion, "6"))
            };

            foreach (var (target, path) in fileNames)
            {
                _logger.LogInformation("{target} GeoIP file to be written to {path}", target, path);
            }

            if (fileNames.Values.All(File.Exists))
            {
                _logger.LogInformation("GeoIP files already exist, skipping write");
                goto returnResult;
            }

            var writers = fileNames.ToDictionary(x => x.Key, x => new GeoIpWriter(x.Value));

            try
            {
                await Task.WhenAll(writers.Values.Select(x => x.WriteHeader(database))).ConfigureAwait(false);

                foreach (var country in database.Countries)
                {
                    cancellation.ThrowIfCancellationRequested();

                    _logger.LogDebug("Writing GeoIP data for {country}", country.CountryName);

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

            // delete all old geoip files from temp
            foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), string.Format(GeoIpFileTemplate, "*", "*"), SearchOption.TopDirectoryOnly))
            {
                if (fileNames.Values.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                _logger.LogInformation("Deleting old GeoIP file {file}", Path.GetFileName(file));
                File.Delete(file);
            }

            returnResult:
            return fileNames.ToFrozenDictionary(x => x.Key, x => new FileInfo(x.Value));
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
            if (File.Exists(_databasePath) && fileLastModified - DateTimeOffset.Now < TimeSpan.FromHours(12))
            {
                return;
            }

            using (var databaseStream = new FileStream(_databasePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                State = DatabaseState.Downloading;
                _logger.LogInformation("Downloading onion.db (expiry date: {ExpiryDate})...", fileLastModified);

                // todo add progress tracking, handle errors from PerformDownload

                var onionDbRequest = new OnionDbDownloadRequest(fileLastModified);
                var downloadRequestStatus = await _client.PerformDownload(onionDbRequest, databaseStream, null, true, false, _cancellation.Token).ConfigureAwait(false);

                // return if download wasn't successful (no file to reload)
                _logger.LogInformation("onion.db download request returned {status}", downloadRequestStatus);
                if (downloadRequestStatus != HttpStatusCode.OK)
                {
                    return;
                }
            }

            LoadLocalDatabase();
        }

        private void LoadLocalDatabase()
        {
            if (!File.Exists(_databasePath))
            {
                _currentDb = null;
                return;
            }

            State = DatabaseState.Processing;

            using (var localReadStream = new FileStream(_databasePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
            {
                _logger.LogInformation("Reading onion.db from disk");
                _currentDb = OnionDb.Parser.ParseFrom(localReadStream);
            }

            GeoIPFiles = PrepareGeoIpFiles(_currentDb, _cancellation.Token);
            Countries = _currentDb.Countries.Select(x => new TorNodeCountry(x.CountryName, x.CountryCode, x.EntryNodeCount, x.ExitNodeCount, x.TotalNodeCount)).ToList();

            State = DatabaseState.Ready;
        }

        private void ValidateCountrySelection(OnionFruitSetting key, string currentValue)
        {
            var country = Countries.SingleOrDefault(x => x.CountryCode == currentValue);

            switch (key)
            {
                // check that the entry/exit count is positive while ensuring the country exists
                case OnionFruitSetting.TorExitCountryCode when country?.ExitNodeCount is null or 0:
                case OnionFruitSetting.TorEntryCountryCode when country?.EntryNodeCount is null or 0:
                    _settings.SetValue(key, IOnionDatabase.TorCountryCode);
                    break;
            }
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

        public void Dispose()
        {
            _checkTimer?.Dispose();
            _currentCheckTask?.Dispose();
            _cancellation?.Dispose();

            GeoIPFiles?.Dispose();
        }
    }
}