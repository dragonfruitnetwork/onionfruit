// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Data;
using DragonFruit.OnionFruit.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Database
{
    /// <summary>
    /// Hosted service responsible for managing the onion.db and geoip files
    /// </summary>
    public class OnionDbService : IOnionDatabase, IHostedService, IDisposable
    {
        private const string GeoIpFileTemplate = "oniondb-{0}.geoip{1}";

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


        private readonly ApiClient _client;
        private readonly ILogger<OnionDbService> _logger;

        private readonly FileSystemWatcher _fileWatcher = new()
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Filter = string.Format(GeoIpFileTemplate, "*", "*"),
            Path = Path.GetTempPath(),
            EnableRaisingEvents = false
        };

        public OnionDbService(ApiClient client, ILogger<OnionDbService> logger)
        {
            _client = client;
            _logger = logger;

            _fileWatcher.Deleted += GeoIpFileAltered;
            _fileWatcher.Changed += GeoIpFileAltered;
            _fileWatcher.Renamed += GeoIpFileAltered;
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

            using (var databaseStream = new FileStream(DatabasePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
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

            _fileWatcher.EnableRaisingEvents = false;
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
            _fileWatcher.EnableRaisingEvents = true;
            return fileNames.ToFrozenDictionary(x => x.Key, x => new FileInfo(x.Value));
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
                _logger.LogInformation("Reading onion.db from disk");
                _currentDb = OnionDb.Parser.ParseFrom(localReadStream);
            }

            GeoIpFileAltered(this, null);
            Countries = _currentDb.Countries.Select(x => new TorNodeCountry(x.CountryName, x.CountryCode, x.EntryNodeCount, x.ExitNodeCount, x.TotalNodeCount)).ToList();

            State = DatabaseState.Ready;
        }

        private void GeoIpFileAltered(object sender, FileSystemEventArgs args)
        {
            if (args != null)
            {
                var filename = args is RenamedEventArgs renArgs ? renArgs.OldFullPath : args.FullPath;

                if (GeoIPFiles.IsCompletedSuccessfully && GeoIPFiles.Result.Values.All(x => x.FullName != filename))
                {
                    return;
                }
            }

            if (_currentDb == null || GeoIPFiles?.Status is TaskStatus.WaitingForActivation or TaskStatus.WaitingToRun or TaskStatus.Running)
            {
                return;
            }

            GeoIPFiles = PrepareGeoIpFiles(_currentDb, _cancellation.Token);
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
            _fileWatcher.EnableRaisingEvents = false;

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
            _fileWatcher?.Dispose();

            GeoIPFiles?.Dispose();
        }
    }
}