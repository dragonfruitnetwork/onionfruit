// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using DragonFruit.OnionFruit.Database;
using Microsoft.Extensions.Logging;
using CodedOutputStream = Google.Protobuf.CodedOutputStream;

namespace DragonFruit.OnionFruit.Configuration
{
    public class OnionFruitSettingsStore : SettingsStore<OnionFruitSetting>
    {
        private const int ConfigVersion = 1;

        private readonly ILogger<OnionFruitSettingsStore> _logger;
        private readonly List<Action<OnionFruitConfigFile>> _valueApplicators = [];

        private OnionFruitConfigFile _configFile;

        private string ConfigurationFile => Path.Combine(App.StoragePath, "onionfruit.cfg");

        public OnionFruitSettingsStore(ILogger<OnionFruitSettingsStore> logger)
        {
            _logger = logger;

            RegisterSettings();
            LoadConfiguration();

            IsLoaded.OnNext(true);
        }

        public Version PreviousClientVersion { get; private set; }

        protected override void RegisterSettings()
        {
            // todo use the Has* property to determine if it's been set, if so use getter otherwise use default
            RegisterOption(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode, static c => c.EntryCountryCode, static (c, val) => c.EntryCountryCode = val ?? IOnionDatabase.TorCountryCode);
            RegisterOption(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode, static c => c.ExitCountryCode, static (c, val) => c.ExitCountryCode = val ?? IOnionDatabase.TorCountryCode);
        }

        protected override void LoadConfiguration()
        {
            if (File.Exists(ConfigurationFile))
            {
                using var fs = File.OpenRead(ConfigurationFile);
                _configFile = OnionFruitConfigFile.Parser.ParseFrom(fs);
            }
            else
            {
                _configFile = new OnionFruitConfigFile()
                {
                    ConfigVersion = ConfigVersion
                };
            }

            if (!string.IsNullOrEmpty(_configFile.LastClientVersion) && Version.TryParse(_configFile.LastClientVersion, out var v))
            {
                PreviousClientVersion = v;
            }

            var currentVersion = typeof(App).Assembly.GetName().Version!;
            if (PreviousClientVersion != currentVersion)
            {
                _configFile.LastClientVersion = currentVersion.ToString();

                SaveConfiguration();
            }

            foreach (var applicator in _valueApplicators)
            {
                applicator.Invoke(_configFile);
            }
        }

        protected override void SaveConfiguration()
        {
            // create a backup copy of the current file
            if (File.Exists(ConfigurationFile))
            {
                File.Copy(ConfigurationFile, ConfigurationFile + ".bak", true);
            }

            using var codedOutputStream = new CodedOutputStream(File.Create(ConfigurationFile));
            _configFile.WriteTo(codedOutputStream);
        }

        private void RegisterOption<T>(OnionFruitSetting key, T defaultValue, Func<OnionFruitConfigFile, T> getter, Action<OnionFruitConfigFile, T> setter)
        {
            var observable = RegisterOption(key, defaultValue, out var subject);

            _valueApplicators.Add(c => subject.OnNext(getter.Invoke(c)));
            Subscriptions.Add(observable.Subscribe(value =>
            {
                _logger.LogDebug("Configuration value {key} updated to {value}", key, value);
                setter.Invoke(_configFile, value);
            }));
        }
    }

    public enum OnionFruitSetting
    {
        TorEntryCountryCode,
        TorExitCountryCode
    }
}