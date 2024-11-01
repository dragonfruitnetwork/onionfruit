// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace DragonFruit.OnionFruit.Updater
{
    public class VelopackUpdater : IOnionFruitUpdater, IHostedService, IDisposable
    {
        private readonly ILogger<VelopackUpdater> _logger;
        private readonly UpdateManager _updateManager;
        private readonly SemaphoreSlim _semaphore;

        private CancellationTokenSource _cancellation;
        private Timer _checkTimer;

        private OnionFruitUpdaterStatus _status;
        private int? _downloadProgress;

        public VelopackUpdater(UpdateOptions options, ILogger<VelopackUpdater> logger)
        {
            _logger = logger;
            _semaphore = new SemaphoreSlim(1, 1);
            _updateManager = new UpdateManager(new GithubSource("https://github.com/aspriddell/onionfruit-xplat", null, true), options);
        }

        public OnionFruitUpdaterStatus Status
        {
            get => _status;
            private set
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public int? DownloadProgress
        {
            get => _downloadProgress;
            private set
            {
                _downloadProgress = value;
                DownloadProgressChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<OnionFruitUpdaterStatus> StatusChanged;
        public event EventHandler<int?> DownloadProgressChanged;

        public async Task TriggerUpdateCheck()
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                throw new InvalidOperationException("An update check is already in progress");
            }

            try
            {
                _cancellation?.Dispose();
                _cancellation = new CancellationTokenSource();

                Status = OnionFruitUpdaterStatus.Checking;

                var updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (updateInfo == null)
                {
                    Status = OnionFruitUpdaterStatus.UpToDate;
                    return;
                }

                DownloadProgress = null;
                Status = OnionFruitUpdaterStatus.Downloading;

                await _updateManager.DownloadUpdatesAsync(updateInfo, p => DownloadProgress = p).ConfigureAwait(false);

                Status = OnionFruitUpdaterStatus.PendingRestart;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to perform updater: {message}", e.Message);
                Status = OnionFruitUpdaterStatus.Failed;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AppExitCallback(bool restart)
        {
            if (_updateManager.UpdatePendingRestart != null)
            {
                await _updateManager.WaitExitThenApplyUpdatesAsync(_updateManager.UpdatePendingRestart, true, restart).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Returns the update channel name for the current platform/selected release stream.
        /// </summary>
        internal static string UpdateChannelName(string prefix, UpdateStream? stream) => stream switch
        {
            UpdateStream.Stable => prefix,
            UpdateStream.Beta => $"{prefix}-beta",

            // use whatever velopack has chosen
            _ => null
        };

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            if (_updateManager.IsInstalled)
            {
                _checkTimer = new Timer(_ => TriggerUpdateCheck(), null, TimeSpan.Zero, TimeSpan.FromHours(12));
            }
            else
            {
                Status = OnionFruitUpdaterStatus.UpToDate;
            }

            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _checkTimer?.Dispose();
            _cancellation?.Cancel();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _checkTimer?.Dispose();
            _semaphore?.Dispose();
        }
    }
}