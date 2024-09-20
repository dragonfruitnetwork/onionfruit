// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Core.Transports;
using DragonFruit.OnionFruit.Database;
using DragonFruit.OnionFruit.Models;
using DynamicData;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using ReactiveUI;

namespace DragonFruit.OnionFruit.ViewModels
{
    public class ConnectionSettingsTabViewModel : ReactiveObject, IDisposable
    {
        private readonly OnionDbService _database;
        private readonly OnionFruitSettingsStore _settings;

        private ushort? _firewallPortValue;

        private readonly CompositeDisposable _disposables = new();

        private readonly ObservableAsPropertyHelper<bool> _databaseLoaded;
        private readonly ObservableAsPropertyHelper<DatabaseState> _databaseState;

        private readonly ReadOnlyObservableCollection<uint> _allowedFirewallPorts;
        private readonly ObservableAsPropertyHelper<bool> _enableFirewallRestrictions, _showFirewallPortsList;

        private readonly ObservableAsPropertyHelper<bool> _disconnectOnTorFailure;

        private readonly ObservableAsPropertyHelper<bool> _canSelectEntryCountry;
        private readonly ObservableAsPropertyHelper<string> _selectedEntryCountryFlag, _selectedExitCountryFlag;
        private readonly ObservableAsPropertyHelper<TorNodeCountry> _selectedEntryCountry, _selectedExitCountry;
        private readonly ObservableAsPropertyHelper<IEnumerable<TorNodeCountry>> _entryCountries, _exitCountries;

        public ConnectionSettingsTabViewModel(OnionDbService database, OnionFruitSettingsStore settings)
        {
            _database = database;
            _settings = settings;

            var databaseState = Observable.FromEventPattern<EventHandler<DatabaseState>, DatabaseState>(handler => database.StateChanged += handler, handler => database.StateChanged -= handler)
                .StartWith(new EventPattern<DatabaseState>(this, database.State))
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

            _entryCountries = countries.Select(x => x.entryCountries).ToProperty(this, x => x.EntryCountries).DisposeWith(_disposables);
            _exitCountries = countries.Select(x => x.exitCountries).ToProperty(this, x => x.ExitCountries).DisposeWith(_disposables);

            _databaseState = databaseState.Select(x => x.EventArgs).ToProperty(this, x => x.DatabaseState).DisposeWith(_disposables);
            _databaseLoaded = databaseState.Select(x => x.EventArgs == DatabaseState.Ready).ToProperty(this, x => x.DatabaseLoaded).DisposeWith(_disposables);

            // settings binding
            var entryCountry = this.WhenAnyValue(x => x.EntryCountries)
                .Where(x => x != null)
                .CombineLatest(settings.GetObservableValue<string>(OnionFruitSetting.TorEntryCountryCode))
                .Select(x => x.First?.SingleOrDefault(y => y.CountryCode == x.Second))
                .ObserveOn(RxApp.MainThreadScheduler);

            var exitCountry = this.WhenAnyValue(x => x.ExitCountries)
                .Where(x => x != null)
                .CombineLatest(settings.GetObservableValue<string>(OnionFruitSetting.TorExitCountryCode))
                .Select(x => x.First?.SingleOrDefault(y => y.CountryCode == x.Second))
                .ObserveOn(RxApp.MainThreadScheduler);

            var firewallPorts = settings.GetCollection<uint>(OnionFruitSetting.AllowedFirewallPorts);

            _canSelectEntryCountry = settings.GetObservableValue<TransportType>(OnionFruitSetting.SelectedTransportType)
                .Select(x => x == TransportType.None)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CanSelectEntryCountry)
                .DisposeWith(_disposables);

            // versions/licenses are updated when the database state changes
            databaseState.Subscribe(s =>
            {
                if (s.EventArgs != DatabaseState.Ready)
                {
                    return;
                }

                this.RaisePropertyChanged(nameof(DatabaseVersion));
                this.RaisePropertyChanged(nameof(DatabaseLicense));
            }).DisposeWith(_disposables);

            _showFirewallPortsList = firewallPorts.CountChanged.Select(x => x > 0)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ShouldShowFirewallPortList)
                .DisposeWith(_disposables);

            firewallPorts.Connect()
                .Sort(Comparer<uint>.Default)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _allowedFirewallPorts)
                .Subscribe()
                .DisposeWith(_disposables);

            _enableFirewallRestrictions = settings.GetObservableValue<bool>(OnionFruitSetting.EnableFirewallPortRestrictions)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.EnableRestrictedFirewallMode)
                .DisposeWith(_disposables);

            _disconnectOnTorFailure = settings.GetObservableValue<bool>(OnionFruitSetting.DisconnectOnTorFailure)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.DisconnectOnTorFailure)
                .DisposeWith(_disposables);

            _selectedEntryCountry = entryCountry.ToProperty(this, x => x.SelectedEntryCountry).DisposeWith(_disposables);
            _selectedExitCountry = exitCountry.ToProperty(this, x => x.SelectedExitCountry).DisposeWith(_disposables);

            _selectedEntryCountryFlag = entryCountry.Select(GetFlagEmoji).ToProperty(this, x => x.SelectedEntryCountryFlag).DisposeWith(_disposables);
            _selectedExitCountryFlag = exitCountry.Select(GetFlagEmoji).ToProperty(this, x => x.SelectedExitCountryFlag).DisposeWith(_disposables);

            AddFirewallPort = ReactiveCommand.Create(AddFirewallPortImpl, this.WhenAnyValue(x => x.FirewallPort).Select(x => x.HasValue));
            RemoveFirewallPort = ReactiveCommand.Create<uint>(RemoveFirewallPortImpl);
        }

        public IconSource DatabaseStateIcon => App.GetIcon(LucideIconNames.DatabaseZap);
        public IconSource EntryLocationIcon => App.GetIcon(LucideIconNames.LandPlot);
        public IconSource ExitLocationIcon => App.GetIcon(LucideIconNames.Earth);
        public IconSource FirewallIcon => App.GetIcon(LucideIconNames.Construction);
        public IconSource KillswitchIcon => App.GetIcon(LucideIconNames.Unplug);

        /// <summary>
        /// Gets whether the countries database has been loaded and <see cref="TorNodeCountry"/> items have been created.
        /// </summary>
        public bool DatabaseLoaded => _databaseLoaded.Value;

        /// <summary>
        /// More verbose state of the database
        /// </summary>
        public DatabaseState DatabaseState => _databaseState.Value;

        public string DatabaseVersion => _database.DisplayVersion;
        public string DatabaseLicense => _database.EmbeddedLicenses;

        /// <summary>
        /// The currently available "guard" nodes, represented as countries
        /// </summary>
        public IEnumerable<TorNodeCountry> EntryCountries => _entryCountries.Value;

        /// <summary>
        /// The currently available exit nodes, grouped by residing country
        /// </summary>
        public IEnumerable<TorNodeCountry> ExitCountries => _exitCountries.Value;

        /// <summary>
        /// Whether an entry country can be selected (bridges are not in use)
        /// </summary>
        public bool CanSelectEntryCountry => _canSelectEntryCountry.Value;

        /// <summary>
        /// Whether the firewall ports list should be shown (i.e. it has items to present)
        /// </summary>
        public bool ShouldShowFirewallPortList => _showFirewallPortsList.Value;

        /// <summary>
        /// Publicly exposed binding for the allowed firewall ports
        /// </summary>
        public ReadOnlyObservableCollection<uint> AllowedFirewallPorts => _allowedFirewallPorts;

        public ICommand AddFirewallPort { get; }
        public ICommand RemoveFirewallPort { get; }

        public ushort? FirewallPort
        {
            get => _firewallPortValue;
            set => this.RaiseAndSetIfChanged(ref _firewallPortValue, value);
        }

        public bool EnableRestrictedFirewallMode
        {
            get => _enableFirewallRestrictions.Value;
            set => _settings.SetValue(OnionFruitSetting.EnableFirewallPortRestrictions, value);
        }

        public bool DisconnectOnTorFailure
        {
            get => _disconnectOnTorFailure.Value;
            set => _settings.SetValue(OnionFruitSetting.DisconnectOnTorFailure, value);
        }

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

        public string SelectedEntryCountryFlag => _selectedEntryCountryFlag.Value;
        public string SelectedExitCountryFlag => _selectedExitCountryFlag.Value;

        private static string GetFlagEmoji(TorNodeCountry country)
        {
            // use globe if not known
            if (country.CountryCode == IOnionDatabase.TorCountryCode)
            {
                return "\U0001F6A9";
            }

            var normalised = country.CountryCode.ToUpperInvariant();

            int firstCodePoint = 0x1F1E6 + (normalised[0] - 'A');
            int secondCodePoint = 0x1F1E6 + (normalised[1] - 'A');

            return char.ConvertFromUtf32(firstCodePoint) + char.ConvertFromUtf32(secondCodePoint);
        }

        private void AddFirewallPortImpl()
        {
            var port = FirewallPort ?? 0;

            if (port == 0)
            {
                return;
            }

            _settings.GetCollection<uint>(OnionFruitSetting.AllowedFirewallPorts).Edit(list =>
            {
                if (list.Contains(port))
                {
                    return;
                }

                list.AddRange([port]);
            });

            FirewallPort = null;
        }

        private void RemoveFirewallPortImpl(uint port)
        {
            _settings.GetCollection<uint>(OnionFruitSetting.AllowedFirewallPorts).Remove(port);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}