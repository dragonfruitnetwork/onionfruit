// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core.Transports;
using DragonFruit.OnionFruit.Services;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class BridgeSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionFruitSettingsStore _settings;
        private readonly TransportManager _transports;

        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<TransportType> _selectedTransport;

        public BridgeSettingsTabViewModel(OnionFruitSettingsStore settings, TransportManager transports)
        {
            _settings = settings;
            _transports = transports;

            _selectedTransport = settings.GetObservableValue<TransportType>(OnionFruitSetting.SelectedTransportType)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedTransport)
                .DisposeWith(_disposables);

            var transportOptions = new Dictionary<TransportType, string>
            {
                [TransportType.None] = "Disabled"
            };

            foreach (var transport in transports.AvailableTransportTypes)
            {
                transportOptions.Add(transport, transport.ToString());
            }

            AvailableTransports = transportOptions;
        }

        public TransportType SelectedTransport
        {
            get => _selectedTransport.Value;
            set => _settings.SetValue(OnionFruitSetting.SelectedTransportType, value);
        }

        public IReadOnlyDictionary<TransportType, string> AvailableTransports { get; }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}