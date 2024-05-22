// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Database;
using CodedOutputStream = Google.Protobuf.CodedOutputStream;

namespace DragonFruit.OnionFruit.Configuration
{
    public class OnionFruitSettingsStore : SettingsStore<OnionFruitSetting>
    {
        private readonly List<Action<OnionFruitConfigFile>> _valueApplicators = [];
        private OnionFruitConfigFile _configFile;

        private string ConfigurationFile => Path.Combine(App.StoragePath, "onionfruit.cfg");

        public OnionFruitSettingsStore()
        {
            RegisterOption(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode, static c => c.EntryCountryCode, static (c, val) => c.EntryCountryCode = val);
            RegisterOption(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode, static c => c.ExitCountryCode, static (c, val) => c.ExitCountryCode = val);
        }

        protected override async Task LoadConfiguration()
        {
            if (File.Exists(ConfigurationFile))
            {
                var configBytes = await File.ReadAllBytesAsync(ConfigurationFile).ConfigureAwait(false);
                _configFile = OnionFruitConfigFile.Parser.ParseFrom(configBytes);
            }
            else
            {
                _configFile = new OnionFruitConfigFile();
            }

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
            Subscriptions.Add(subject.DistinctUntilChanged().Subscribe(value => setter.Invoke(_configFile, value)));

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