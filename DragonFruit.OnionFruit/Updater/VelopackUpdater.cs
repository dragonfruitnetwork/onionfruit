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

        private CancellationTokenSource _cancellation;
        private Timer _checkTimer;
        private Task _updateTask;

        private OnionFruitUpdaterStatus _status;
        private int? _downloadProgress;

        public VelopackUpdater(UpdateOptions options, ILogger<VelopackUpdater> logger)
        {
            _logger = logger;
            _updateManager = new UpdateManager(new GithubSource("https://github.com/dragonfruitnetwork/onionfruit", null, true), options);
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

        public bool IsInstalled => _updateManager.IsInstalled && !_updateManager.IsPortable;

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

        public Task TriggerUpdateCheck()
        {
            lock (this)
            {
                if (_updateTask?.IsCompleted != false)
                {
                    _updateTask = PerformUpdateCheckInternal();
                }

                return _updateTask;
            }
        }

        private async Task PerformUpdateCheckInternal()
        {
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();

            try
            {
                Status = OnionFruitUpdaterStatus.Checking;

                var updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (updateInfo != null)
                {
                    DownloadProgress = null;
                    Status = OnionFruitUpdaterStatus.Downloading;

                    await _updateManager.DownloadUpdatesAsync(updateInfo, p => DownloadProgress = p, cancelToken: _cancellation.Token).ConfigureAwait(false);
                }

                Status = _updateManager.UpdatePendingRestart != null
                    ? OnionFruitUpdaterStatus.PendingRestart
                    : OnionFruitUpdaterStatus.UpToDate;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to perform update: {message}", e.Message);
                Status = OnionFruitUpdaterStatus.Failed;
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
            _ => prefix
        };

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            if (_updateManager.IsInstalled)
            {
                _checkTimer = new Timer(_ => TriggerUpdateCheck(), null, TimeSpan.Zero, TimeSpan.FromHours(12));
            }
            else
            {
                Status = OnionFruitUpdaterStatus.Disabled;
            }

            return Task.CompletedTask;
        }

        async Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            if (_cancellation != null)
            {
                if (!_cancellation.IsCancellationRequested)
                {
                    await _cancellation.CancelAsync();
                }

                _cancellation.Dispose();
            }

            if (_checkTimer != null)
            {
                await _checkTimer.DisposeAsync();
            }

            _checkTimer = null;
            _cancellation = null;

            if (_updateManager.UpdatePendingRestart != null)
            {
                await _updateManager.WaitExitThenApplyUpdatesAsync(_updateManager.UpdatePendingRestart, true, false);
            }
        }

        public void Dispose()
        {
            _checkTimer?.Dispose();
            _cancellation?.Dispose();
        }
    }
}