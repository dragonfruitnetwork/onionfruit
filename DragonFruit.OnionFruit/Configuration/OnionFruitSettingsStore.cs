// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Database;
using Microsoft.Extensions.Logging;
using CodedOutputStream = Google.Protobuf.CodedOutputStream;

namespace DragonFruit.OnionFruit.Configuration
{
    public class OnionFruitSettingsStore : SettingsStore<OnionFruitSetting>
    {
        private readonly OnionFruitConfigFile _configFile;
        private readonly ILogger<OnionFruitSettingsStore> _logger;
        private readonly List<Action<OnionFruitConfigFile>> _valueApplicators = [];

        private string ConfigurationFile => Path.Combine(App.StoragePath, "onionfruit.cfg");

        public OnionFruitSettingsStore(ILogger<OnionFruitSettingsStore> logger)
        {
            _logger = logger;
            _configFile = File.Exists(ConfigurationFile) ? OnionFruitConfigFile.Parser.ParseFrom(File.ReadAllBytes(ConfigurationFile)) : new OnionFruitConfigFile();

            RegisterOption(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode, static c => c.EntryCountryCode, static (c, val) => c.EntryCountryCode = val ?? IOnionDatabase.TorCountryCode);
            RegisterOption(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode, static c => c.ExitCountryCode, static (c, val) => c.ExitCountryCode = val ?? IOnionDatabase.TorCountryCode);

            UpdateRegisteredValues();
        }

        protected override Task SaveConfiguration()
        {
            using var codedOutputStream = new CodedOutputStream(File.Create(ConfigurationFile));

            _configFile.WriteTo(codedOutputStream);
            return Task.CompletedTask;
        }

        protected void RegisterOption<T>(OnionFruitSetting key, T defaultValue, Func<OnionFruitConfigFile, T> getter, Action<OnionFruitConfigFile, T> setter)
        {
            if (ConfigStore.ContainsKey(key))
            {
                throw new ArgumentException($"Key {key} already exists in the configuration store");
            }

            var subject = new BehaviorSubject<T>(defaultValue);

            ConfigStore.Add(key, subject);
            Subscriptions.Add(subject.DistinctUntilChanged().Subscribe(value =>
            {
                _logger.LogDebug("Configuration value {key} updated to {value}", key, value);
                setter.Invoke(_configFile, value);
            }));

            _valueApplicators.Add(c => subject.OnNext(getter.Invoke(c)));
        }

        /// <summary>
        /// Invokes all getters against the current configuration object to update stored values
        /// </summary>
        private void UpdateRegisteredValues()
        {
            foreach (var applicator in _valueApplicators)
            {
                applicator.Invoke(_configFile);
            }
        }
    }

    public enum OnionFruitSetting
    {
        TorEntryCountryCode,
        TorExitCountryCode
    }
}