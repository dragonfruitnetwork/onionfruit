// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class ConnectionSettingsTabViewModel : ReactiveObject
    {
        private readonly OnionFruitSettingsStore _settings;

        private readonly ObservableAsPropertyHelper<bool> _databaseLoaded;
        private readonly ObservableAsPropertyHelper<TorNodeCountry> _selectedEntryCountry, _selectedExitCountry;
        private readonly ObservableAsPropertyHelper<IEnumerable<TorNodeCountry>> _entryCountries, _exitCountries;

        public ConnectionSettingsTabViewModel(OnionDbService database, OnionFruitSettingsStore settings)
        {
            _settings = settings;
            var databaseReady = Observable.FromEventPattern<EventHandler<DatabaseState>, DatabaseState>(handler => database.StateChanged += handler, handler => database.StateChanged -= handler)
                .StartWith(new EventPattern<DatabaseState>(this, database.State))
                .Select(x => x.EventArgs == DatabaseState.Ready)
                .ObserveOn(RxApp.MainThreadScheduler);

            var countries = Observable.FromEventPattern<EventHandler<IReadOnlyCollection<TorNodeCountry>>, IReadOnlyCollection<TorNodeCountry>>(handler => database.CountriesChanged += handler, handler => database.CountriesChanged -= handler)
                .StartWith(new EventPattern<IReadOnlyCollection<TorNodeCountry>>(this, database.Countries))
                .Select(x =>
                {
                    // split into two lists, ones with more than one entry server and ones with more than one exit server
                    var entryCountries = new List<TorNodeCountry>();
                    var exitCountries = new List<TorNodeCountry>();

                    foreach (var country in x.EventArgs)
                    {
                        if (country.EntryNodeCount >= 1)
                        {
                            entryCountries.Add(country);
                        }

                        if (country.ExitNodeCount >= 1)
                        {
                            exitCountries.Add(country);
                        }
                    }

                    entryCountries.Sort(TorNodeCountry.TorNodeCountryNameComparer.Instance);
                    exitCountries.Sort(TorNodeCountry.TorNodeCountryNameComparer.Instance);

                    entryCountries.Insert(0, TorNodeCountry.Random);
                    exitCountries.Insert(0, TorNodeCountry.Random);

                    return (entryCountries, exitCountries);
                })
                .ObserveOn(RxApp.MainThreadScheduler);

            _entryCountries = countries.Select(x => x.entryCountries).ToProperty(this, x => x.EntryCountries);
            _exitCountries = countries.Select(x => x.exitCountries).ToProperty(this, x => x.ExitCountries);
            _databaseLoaded = databaseReady.ToProperty(this, x => x.DatabaseLoaded);

            // settings binding
            _selectedEntryCountry = this.WhenAnyValue(x => x.EntryCountries)
                .Where(x => x != null)
                .CombineLatest(settings.GetObservableValue<string>(OnionFruitSetting.TorEntryCountryCode))
                .Select(x => x.First?.SingleOrDefault(y => y.CountryCode == x.Second))
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedEntryCountry);

            _selectedExitCountry = this.WhenAnyValue(x => x.ExitCountries)
                .Where(x => x != null)
                .CombineLatest(settings.GetObservableValue<string>(OnionFruitSetting.TorExitCountryCode))
                .Select(x => x.First?.SingleOrDefault(y => y.CountryCode == x.Second))
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedExitCountry);
        }

        /// <summary>
        /// Gets whether the countries database has been loaded and <see cref="TorNodeCountry"/> items have been created.
        /// </summary>
        public bool DatabaseLoaded => _databaseLoaded.Value;

        /// <summary>
        /// The currently available "guard" nodes, represented as countries
        /// </summary>
        public IEnumerable<TorNodeCountry> EntryCountries => _entryCountries.Value;

        /// <summary>
        /// The currently available exit nodes, grouped by residing country
        /// </summary>
        public IEnumerable<TorNodeCountry> ExitCountries => _exitCountries.Value;

        public TorNodeCountry SelectedEntryCountry
        {
            get => _selectedEntryCountry.Value;
            set
            {
                if (!_databaseLoaded.Value)
                {
                    return;
                }

                _settings.SetValue(OnionFruitSetting.TorEntryCountryCode, value.CountryCode);
            }
        }

        public TorNodeCountry SelectedExitCountry
        {
            get => _selectedExitCountry.Value;
            set
            {
                if (!_databaseLoaded.Value)
                {
                    return;
                }

                _settings.SetValue(OnionFruitSetting.TorExitCountryCode, value.CountryCode);
            }
        }
    }
}