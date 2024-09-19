// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core.Config;
using DragonFruit.OnionFruit.Core.Transports;
using DragonFruit.OnionFruit.Services;
using DynamicData;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class BridgeSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly TransportManager _transports;

        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<bool> _canAddBridgeLines;
        private readonly ObservableAsPropertyHelper<TransportType> _selectedTransport;

        private readonly ReadOnlyObservableCollection<string> _activeTransportBridgeLines;

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

            settings.GetCollection<string>(OnionFruitSetting.UserDefinedBridges)
                .Connect()
                .Filter(selectedTransport.Select(BuildBridgeLineFilter))
                .Bind(out _activeTransportBridgeLines)
                .Subscribe()
                .DisposeWith(_disposables);
        }

        public IReadOnlyDictionary<TransportType, string> AvailableTransports { get; }

        public TransportType SelectedTransport
        {
            get => _selectedTransport.Value;
            set => _settings.SetValue(OnionFruitSetting.SelectedTransportType, value);
        }

        public bool CanAddBridgeLines => _canAddBridgeLines.Value;
        public IEnumerable<string> CurrentBridgeLines => _activeTransportBridgeLines;

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

                // todo cache result?
                var lineMatch = BridgeEntry.ValidationRegex().Match(line);
                return lineMatch.Success && lineMatch.Groups["type"].Value.Equals(typeInfo.Id, StringComparison.Ordinal);
            };
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}