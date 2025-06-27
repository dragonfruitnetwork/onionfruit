// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        private const int MaxGeoIpFileAge = 48;

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

        public event EventHandler<DatabaseState> StateChanged;
        public event EventHandler<IReadOnlyCollection<TorNodeCountry>> CountriesChanged;

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

        /// <summary>
        /// The version to display to the user.
        /// </summary>
        [MaybeNull]
        public string DisplayVersion { get; private set; }

        /// <summary>
        /// Any embedded licenses included with the database.
        /// </summary>
        [MaybeNull]
        public string EmbeddedLicenses { get; private set; }

        public Task<IReadOnlyDictionary<AddressFamily, FileInfo>> GeoIPFiles { get; private set; }

        private void TimerPinged()
        {
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

            var writers = fileNames.ToFrozenDictionary(x => x.Key, x => new GeoIpWriter(x.Value));

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

            // skip download if file is new enough
            if (DateTimeOffset.Now - fileLastModified < TimeSpan.FromHours(MaxGeoIpFileAge))
            {
                return;
            }

            State = DatabaseState.Downloading;
            _logger.LogInformation("Downloading onion.db (expiry date: {expiry})...", fileLastModified);

            try
            {
                using var databaseStream = new FileStream(_databasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);

                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _cancellation.Token);

                var downloadRequestStatus = await _client.PerformDownload(new OnionDbDownloadRequest(fileLastModified), databaseStream, null, true, true, linkedCancellation.Token).ConfigureAwait(false);

                switch (downloadRequestStatus)
                {
                    case HttpStatusCode.OK:
                        databaseStream.Seek(0, SeekOrigin.Begin);
                        LoadLocalDatabase(databaseStream);
                        break;

                    case HttpStatusCode.NotModified:
                        _logger.LogInformation("onion.db is up to date (returned {status})", downloadRequestStatus);
                        break;

                    default:
                        _logger.LogWarning("Failed to download onion.db - web request returned {status}", downloadRequestStatus);
                        break;
                }
            }
            catch (IOException e)
            {
                _logger.LogError(e, "Failed to download onion.db due to an I/O error: {message}", e.Message);
            }
            catch (HttpRequestException e)
            {
                _logger.LogWarning(e, "Failed to download onion.db - web request failed with error: {message}", e.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("onion.db download request timed out");
            }
            finally
            {
                // if the download failed but a database is still available, continue using it.
                // otherwise use error state to allow random country selection
                if (_currentDb == null)
                {
                    SetErrorState();
                }
            }
        }

        private void LoadLocalDatabase(Stream stream)
        {
            try
            {
                State = DatabaseState.Processing;

                _currentDb = OnionDb.Parser.ParseFrom(stream);
                _logger.LogInformation("onion.db version {v} loaded successfully", _currentDb.DbVersion);
            }
            catch // todo detect if protobuf is corrupt, delete the file and restart the download
            {
                SetErrorState();
                return;
            }

            DisplayVersion = _currentDb.DbVersion.ToString("X");
            GeoIPFiles = PrepareGeoIpFiles(_currentDb, _cancellation.Token);
            Countries = _currentDb.Countries.Select(x => new TorNodeCountry(x.CountryName, x.CountryCode, x.EntryNodeCount, x.ExitNodeCount, x.TotalNodeCount)).ToList();
            EmbeddedLicenses = string.Join(", ",
            [
                string.IsNullOrEmpty(_currentDb.TorLicense) ? null : $"Tor (Onionoo) data licensed under {_currentDb.TorLicense}",
                string.IsNullOrEmpty(_currentDb.GeoLicense) ? null : $"GeoIP (IPFire/libloc) data licensed under {_currentDb.GeoLicense}"
            ]);

            State = DatabaseState.Ready;
        }

        private void ValidateCountrySelection(OnionFruitSetting key, string currentValue)
        {
            // ignore validation of random country code
            if (currentValue == IOnionDatabase.TorCountryCode)
            {
                return;
            }

            var country = Countries.SingleOrDefault(x => x.CountryCode == currentValue);

            switch (key)
            {
                case OnionFruitSetting.TorExitCountryCode when country?.ExitNodeCount is null or 0:
                case OnionFruitSetting.TorEntryCountryCode when country?.EntryNodeCount is null or 0:
                    _settings.SetValue(key, IOnionDatabase.TorCountryCode);
                    break;
            }
        }

        private void SetErrorState()
        {
            Countries = [];
            GeoIPFiles = Task.FromResult<IReadOnlyDictionary<AddressFamily, FileInfo>>(new Dictionary<AddressFamily, FileInfo>(0));

            // defaults to no countries and no geoip files, but reports to consumers as fully ready (which it technically is)
            State = DatabaseState.Ready;
        }

        #region HostedService

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();

            if (File.Exists(_databasePath))
            {
                using var localReadStream = new FileStream(_databasePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
                LoadLocalDatabase(localReadStream);
            }

            // start timer to reload database at regular intervals
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