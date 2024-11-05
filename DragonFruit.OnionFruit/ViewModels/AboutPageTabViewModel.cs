// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using DragonFruit.OnionFruit.Models;
using DragonFruit.OnionFruit.Services;
using DragonFruit.OnionFruit.Updater;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class AboutPageTabViewModel : ReactiveObject
    {
        private readonly ILogger<AboutPageTabViewModel> _logger;
        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<string> _updateProgress;

        private IEnumerable<NugetPackageLicenseInfo> _packages;

        public AboutPageTabViewModel(IOnionFruitUpdater updater, ILogger<AboutPageTabViewModel> logger)
        {
            _logger = logger;

            var updaterStatus = Observable.FromEventPattern<OnionFruitUpdaterStatus>(h => updater.StatusChanged += h, h => updater.StatusChanged -= h)
                .StartWith(new EventPattern<OnionFruitUpdaterStatus>(this, updater.Status))
                .Select(x => x.EventArgs);

            var updaterProgress = Observable.FromEventPattern<int?>(h => updater.DownloadProgressChanged += h, h => updater.DownloadProgressChanged -= h)
                .StartWith(new EventPattern<int?>(this, updater.DownloadProgress))
                .Select(x => x.EventArgs);

            var canCheckForUpdates = updaterStatus.Select(x => x is not (OnionFruitUpdaterStatus.Checking or OnionFruitUpdaterStatus.Downloading or OnionFruitUpdaterStatus.Disabled))
                .ObserveOn(RxApp.MainThreadScheduler);

            updaterStatus.CombineLatest(updaterProgress)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(x => x.First switch
                {
                    OnionFruitUpdaterStatus.Checking => "Checking for updates...",

                    OnionFruitUpdaterStatus.Downloading when x.Second.HasValue => $"Downloading update ({x.Second}%)",
                    OnionFruitUpdaterStatus.Failed when x.Second.HasValue => "Update failed",

                    OnionFruitUpdaterStatus.Downloading => "Downloading update...",
                    OnionFruitUpdaterStatus.Failed => "Update check failed",

                    OnionFruitUpdaterStatus.UpToDate => "No updates available",
                    OnionFruitUpdaterStatus.PendingRestart => "Pending restart",
                    OnionFruitUpdaterStatus.Disabled => "Updates disabled",

                    _ => throw new ArgumentOutOfRangeException()
                })
                .ToProperty(this, x => x.CurrentUpdaterProgress, out _updateProgress)
                .DisposeWith(_disposables);

            ManualUpdateTrigger = ReactiveCommand.Create(updater.TriggerUpdateCheck, canCheckForUpdates).DisposeWith(_disposables);

            _ = ReadPackageLicenseFile();
        }

        public IconSource UpdaterIcon => App.GetIcon(LucideIconNames.RefreshCw);
        public IconSource LicensesIcon => App.GetIcon(LucideIconNames.Scale);

        public string CurrentUpdaterProgress => _updateProgress.Value;

        public IEnumerable<NugetPackageLicenseInfo> Packages
        {
            get => _packages;
            set => this.RaiseAndSetIfChanged(ref _packages, value);
        }

        public ICommand ManualUpdateTrigger { get; }

        private async Task ReadPackageLicenseFile()
        {
            await Task.Yield();

            var filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), "nuget-licenses.json");

            if (!File.Exists(filePath))
            {
                Packages = [];
                return;
            }

            try
            {
                await using var stream = File.OpenRead(filePath);
                Packages = await JsonSerializer.DeserializeAsync(stream, OnionFruitSerializerContext.Default.IEnumerableNugetPackageLicenseInfo);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to read package license file: {message}", e.Message);

                Packages = [];
            }
        }
    }
}