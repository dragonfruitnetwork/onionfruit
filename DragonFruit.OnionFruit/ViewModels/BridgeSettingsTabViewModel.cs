// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core.Config;
using DragonFruit.OnionFruit.Core.Transports;
using DragonFruit.OnionFruit.Services;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class BridgeSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly TransportManager _transports;

        private readonly IDictionary<string, Match> _matchCache = new Dictionary<string, Match>();

        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<bool> _canAddBridgeLines;
        private readonly ObservableAsPropertyHelper<string> _bridgeLineWatermark;
        private readonly ObservableAsPropertyHelper<TransportType> _selectedTransport;

        private readonly ReadOnlyObservableCollection<string> _activeTransportBridgeLines;

        private string _newBridgeLines;
        private bool _showEmptyBridgeListMessage, _showDefaultsPresetMessage;

        public BridgeSettingsTabViewModel(OnionFruitSettingsStore settings, TransportManager transports)
        {
            _settings = settings;
            _transports = transports;

            var transportOptions = new Dictionary<TransportType, string>
            {
                [TransportType.None] = "Disabled"
            };

            foreach (var transport in transports.AvailableTransportTypes)
            {
                transportOptions.Add(transport, transport.ToString());
            }

            AvailableTransports = transportOptions;

            var selectedTransport = settings.GetObservableValue<TransportType>(OnionFruitSetting.SelectedTransportType);

            _selectedTransport = selectedTransport.ObserveOn(RxApp.MainThreadScheduler).ToProperty(this, x => x.SelectedTransport).DisposeWith(_disposables);
            _canAddBridgeLines = selectedTransport.Select(x => x != TransportType.None).ObserveOn(RxApp.MainThreadScheduler).ToProperty(this, x => x.CanAddBridgeLines).DisposeWith(_disposables);

            _bridgeLineWatermark = selectedTransport
                .Where(x => x != TransportType.None)
                .Select(x => $"{transports.AvailableTransports[x].Id} 0.0.0.0:12345 AAAAAAABBBBBBBCCCCCCDDDDDDEEEEEEFFFFFF".TrimStart())
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.BridgeLineWatermark)
                .DisposeWith(_disposables);

            settings.GetCollection<string>(OnionFruitSetting.UserDefinedBridges)
                .Connect()
                .Filter(selectedTransport.Select(BuildBridgeLineFilter))
                .Bind(out _activeTransportBridgeLines)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(CurrentBridgeLineCount)))
                .DisposeWith(_disposables);

            this.WhenAnyValue(x => x.CurrentBridgeLineCount, x => x.SelectedTransport)
                .Where(x => x.Item2 != TransportType.None)
                .Subscribe(_ => UpdateListMessageVisibility())
                .DisposeWith(_disposables);
        }

        /// <summary>
        /// <see cref="TransportType"/> collection that can be selected by the user
        /// </summary>
        public IReadOnlyDictionary<TransportType, string> AvailableTransports { get; }

        /// <summary>
        /// Whether the user can add bridge lines for the selected transport
        /// </summary>
        public bool CanAddBridgeLines => _canAddBridgeLines.Value;

        /// <summary>
        /// The watermark shown in the textbox for adding new bridge lines
        /// </summary>
        public string BridgeLineWatermark => _bridgeLineWatermark.Value;

        /// <summary>
        /// The bridge lines currently in use for the selected transport, displayed in the UI
        /// </summary>
        public ReadOnlyCollection<string> CurrentBridgeLines => _activeTransportBridgeLines;

        /// <summary>
        /// The number of bridge lines currently in use for the selected transport
        /// </summary>
        public int CurrentBridgeLineCount => _activeTransportBridgeLines.Count;

        public TransportType SelectedTransport
        {
            get => _selectedTransport.Value;
            set => _settings.SetValue(OnionFruitSetting.SelectedTransportType, value);
        }

        /// <summary>
        /// User-defined bridge lines to add to the configuration. Validated on submission
        /// </summary>
        public string NewBridgeLines
        {
            get => _newBridgeLines;
            set => this.RaiseAndSetIfChanged(ref _newBridgeLines, value);
        }

        /// <summary>
        /// Whether the user should be shown a message indicating that the bridge list is empty
        /// </summary>
        public bool ShowEmptyBridgeListMessage
        {
            get => _showEmptyBridgeListMessage;
            private set => this.RaiseAndSetIfChanged(ref _showEmptyBridgeListMessage, value);
        }

        /// <summary>
        /// Whether the "this transport provides some default bridges" message should be shown
        /// </summary>
        public bool ShowDefaultsPresetMessage
        {
            get => _showDefaultsPresetMessage;
            private set => this.RaiseAndSetIfChanged(ref _showDefaultsPresetMessage, value);
        }

        public void AddBridgeLines()
        {
            if (string.IsNullOrEmpty(NewBridgeLines))
            {
                return;
            }

            var lines = NewBridgeLines.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var allowedTypePrefix = _transports.AvailableTransports[SelectedTransport].Id;

            var validLines = new List<string>(lines.Length);
            var invalidLinesStr = new StringBuilder();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var match = BridgeEntry.ValidationRegex().Match(line);

                if (!match.Success || !match.Groups["type"].Value.Equals(allowedTypePrefix, StringComparison.Ordinal))
                {
                    invalidLinesStr.AppendLine(line);
                    continue;
                }

                validLines.Add(line);
            }

            NewBridgeLines = invalidLinesStr.Length > 0 ? invalidLinesStr.ToString().TrimEnd() : string.Empty;
            _settings.GetCollection<string>(OnionFruitSetting.UserDefinedBridges).Edit(l => l.AddRange(validLines.Except(l)));
        }

        public void RemoveBridgeLine(string line)
        {
            _settings.GetCollection<string>(OnionFruitSetting.UserDefinedBridges).Remove(line);
            _matchCache.RemoveIfContained(line);
        }

        private void UpdateListMessageVisibility()
        {
            var bridgeKey = _transports.AvailableTransports[SelectedTransport].DefaultBridgeKey;

            ShowDefaultsPresetMessage = bridgeKey != null
                                        && SelectedTransport != TransportType.None
                                        && CurrentBridgeLineCount == 0
                                        && _transports.Config.Bridges.ContainsKey(bridgeKey);

            ShowEmptyBridgeListMessage = CurrentBridgeLineCount == 0 && !ShowDefaultsPresetMessage;
        }

        private Func<string, bool> BuildBridgeLineFilter(TransportType type)
        {
            if (!_transports.AvailableTransports.TryGetValue(type, out var typeInfo))
            {
                return _ => false;
            }

            return line =>
            {
                if (string.IsNullOrEmpty(line))
                {
                    return false;
                }

                if (!_matchCache.TryGetValue(line, out var match))
                {
                    match = BridgeEntry.ValidationRegex().Match(line);
                    _matchCache.Add(line, match);
                }

                return match.Success && match.Groups["type"].Value.Equals(typeInfo.Id, StringComparison.Ordinal);
            };
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}