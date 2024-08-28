// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive.Disposables;
using System.Reflection;
using DragonFruit.OnionFruit.Database;
using Microsoft.Extensions.Logging;
using CodedOutputStream = Google.Protobuf.CodedOutputStream;

namespace DragonFruit.OnionFruit.Configuration
{
    public class OnionFruitSettingsStore : SettingsStore<OnionFruitSetting>
    {
        private const int ConfigVersion = 1;

        private readonly ILogger<OnionFruitSettingsStore> _logger;

        private OnionFruitConfigFile _configFile;
        private IDictionary<OnionFruitSetting, SettingsStoreEntry> _storeEntries = new Dictionary<OnionFruitSetting, SettingsStoreEntry>();

        private string ConfigurationFile => Path.Combine(App.StoragePath, "onionfruit.cfg");

        public OnionFruitSettingsStore(ILogger<OnionFruitSettingsStore> logger)
        {
            _logger = logger;

            RegisterSettings();
            LoadConfiguration();

            IsLoaded.OnNext(true);
        }

        /// <summary>
        /// Stores Information about a stored settings value.
        /// </summary>
        /// <param name="DefaultValue">The default value, should be stored if the configuration is new or being reset</param>
        /// <param name="Accessor">The <see cref="PropertyInfo"/> to use when getting or setting the underlying value</param>
        /// <param name="ValueClearMethod">Method to use the clear the underlying value in <see cref="Accessor"/>. If this is set, the underlying value can is nullable</param>
        private record SettingsStoreEntry([MaybeNull] object DefaultValue, PropertyInfo Accessor, [MaybeNull] MethodInfo ValueClearMethod, Action<OnionFruitConfigFile> FetchFromConfig);

        public Version PreviousClientVersion { get; private set; }

        protected override void RegisterSettings()
        {
            RegisterOption(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode, nameof(OnionFruitConfigFile.EntryCountryCode));
            RegisterOption(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode, nameof(OnionFruitConfigFile.ExitCountryCode));

            RegisterOption(OnionFruitSetting.EnableWebsiteLaunchConnect, true, nameof(OnionFruitConfigFile.EnableWebsiteLaunchOnConnect));
            RegisterOption(OnionFruitSetting.EnableWebsiteLaunchDisconnect, false, nameof(OnionFruitConfigFile.EnableWebsiteLaunchOnDisconnect));

            RegisterOption<string>(OnionFruitSetting.WebsiteLaunchConnect, null, nameof(OnionFruitConfigFile.LaunchWebsiteOnConnect));
            RegisterOption<string>(OnionFruitSetting.WebsiteLaunchDisconnect, null, nameof(OnionFruitConfigFile.LaunchWebsiteOnDisconnect));

            // freeze to prevent further changes, improve performance
            _storeEntries = _storeEntries.ToFrozenDictionary();
        }

        protected override void LoadConfiguration()
        {
            // todo handle backup file if original is corrupt
            if (File.Exists(ConfigurationFile))
            {
                using var fs = File.OpenRead(ConfigurationFile);
                _configFile = OnionFruitConfigFile.Parser.ParseFrom(fs);

                // read values from config file
                foreach (var entry in _storeEntries.Values)
                {
                    entry.FetchFromConfig.Invoke(_configFile);
                }
            }
            else
            {
                _configFile = new OnionFruitConfigFile
                {
                    ConfigVersion = ConfigVersion
                };

                // write default values to config file
                foreach (var entry in _storeEntries.Values)
                {
                    if (entry.DefaultValue == null && entry.ValueClearMethod != null)
                    {
                        entry.ValueClearMethod.Invoke(_configFile, null);
                    }
                    else
                    {
                        entry.Accessor.SetValue(_configFile, entry.DefaultValue);
                    }
                }
            }

            // set client version info
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

        private void RegisterOption<T>(OnionFruitSetting key, T defaultValue, string propertyName)
        {
            var observable = RegisterOption(key, defaultValue, out var subject);

            var accessor = typeof(OnionFruitConfigFile).GetProperty(propertyName);
            var accessorClearMethod = typeof(OnionFruitConfigFile).GetMethod($"Clear{propertyName}");

            if (accessor == null)
            {
                throw new ArgumentException("Invalid property name", nameof(propertyName));
            }

            if (defaultValue == null && accessorClearMethod == null)
            {
                throw new ArgumentException("Cannot have a null default value without a clear method", nameof(defaultValue));
            }

            _storeEntries[key] = new SettingsStoreEntry(defaultValue, accessor, accessorClearMethod, c => subject.OnNext((T)accessor.GetValue(c, null)));

            observable.Subscribe(value =>
            {
                if (value == null && _storeEntries[key].ValueClearMethod != null)
                {
                    _logger.LogDebug("Configuration value {key} cleared", key);
                    _storeEntries[key].ValueClearMethod.Invoke(_configFile, null);
                }
                else if (value == null)
                {
                    _logger.LogDebug("Configuration value {key} reset to default value ('{val}')", key, defaultValue);
                    subject.OnNext(defaultValue);
                }
                else
                {
                    _logger.LogDebug("Configuration value {key} set to '{val}'", key, value);
                    _storeEntries[key].Accessor.SetValue(_configFile, value);
                }
            }).DisposeWith(Subscriptions);
        }
    }

    public enum OnionFruitSetting
    {
        TorEntryCountryCode,
        TorExitCountryCode,

        EnableWebsiteLaunchConnect,
        EnableWebsiteLaunchDisconnect,

        WebsiteLaunchConnect,
        WebsiteLaunchDisconnect,
    }
}