// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using DragonFruit.OnionFruit.Database;
using DynamicData;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using CodedOutputStream = Google.Protobuf.CodedOutputStream;

namespace DragonFruit.OnionFruit.Configuration
{
    public class OnionFruitSettingsStore : SettingsStore<OnionFruitSetting>
    {
        private const int ConfigVersion = 1;

        private readonly ILogger<OnionFruitSettingsStore> _logger;

        private OnionFruitConfigFile _configFile;

        private IDictionary<OnionFruitSetting, SettingsStoreEntry> _storeEntries = new Dictionary<OnionFruitSetting, SettingsStoreEntry>();
        private IDictionary<OnionFruitSetting, SettingsCollectionEntry> _storeCollections = new Dictionary<OnionFruitSetting, SettingsCollectionEntry>();

        private string ConfigurationFile => Path.Combine(App.StoragePath, "onionfruit.cfg");

        public OnionFruitSettingsStore(ILogger<OnionFruitSettingsStore> logger)
        {
            _logger = logger;

            RegisterSettings();
            LoadConfiguration();

            IsLoaded.OnNext(true);
        }

        /// <summary>
        /// Stores information about a collection of values held in the configuration file
        /// </summary>
        /// <remarks>
        /// Because repeated field properties cannot be replaced (unlike <see cref="SettingsStoreEntry"/>) and have common interfaces regardless of the underlying type,
        /// a <see cref="Func{TResult}"/> can be used to access the underlying value over reflection-based access.
        /// </remarks>
        private record SettingsCollectionEntry(Action<OnionFruitConfigFile> SetFromConfig, Action<OnionFruitConfigFile> SetDefaultValues);

        /// <summary>
        /// Stores information about the accessibility of a setting in the configuration file
        /// </summary>
        /// <param name="DefaultValue">The default value, should be stored if the configuration is new or being reset</param>
        /// <param name="Accessor">The <see cref="PropertyInfo"/> to use when getting or setting the underlying value</param>
        /// <param name="ValueClearMethod">Method to use the clear the underlying value in <see cref="Accessor"/>. If this is set, the underlying value can is nullable</param>
        private record SettingsStoreEntry([MaybeNull] object DefaultValue, PropertyInfo Accessor, [MaybeNull] MethodInfo ValueClearMethod, Action<OnionFruitConfigFile> SetFromConfig);

        public Version PreviousClientVersion { get; private set; }

        protected override void RegisterSettings()
        {
            RegisterOption(OnionFruitSetting.TorEntryCountryCode, IOnionDatabase.TorCountryCode, nameof(OnionFruitConfigFile.EntryCountryCode));
            RegisterOption(OnionFruitSetting.TorExitCountryCode, IOnionDatabase.TorCountryCode, nameof(OnionFruitConfigFile.ExitCountryCode));

            RegisterOption(OnionFruitSetting.EnableWebsiteLaunchConnect, true, nameof(OnionFruitConfigFile.EnableWebsiteLaunchOnConnect));
            RegisterOption(OnionFruitSetting.EnableWebsiteLaunchDisconnect, false, nameof(OnionFruitConfigFile.EnableWebsiteLaunchOnDisconnect));

            RegisterOption<string>(OnionFruitSetting.WebsiteLaunchConnect, null, nameof(OnionFruitConfigFile.LaunchWebsiteOnConnect));
            RegisterOption<string>(OnionFruitSetting.WebsiteLaunchDisconnect, null, nameof(OnionFruitConfigFile.LaunchWebsiteOnDisconnect));

            RegisterOption(OnionFruitSetting.EnableFirewallPortRestrictions, false, nameof(OnionFruitConfigFile.LimitOutboundConnectionPorts));
            RegisterCollection<uint>(OnionFruitSetting.AllowedFirewallPorts, [80, 443], c => c.AllowedFirewallPorts);

            // freeze to prevent further changes
            _storeEntries = _storeEntries.ToFrozenDictionary();
            _storeCollections = _storeCollections.ToFrozenDictionary();
        }

        protected override void LoadConfiguration()
        {
            // todo handle backup file if original is corrupt
            if (File.Exists(ConfigurationFile))
            {
                using (var fs = File.OpenRead(ConfigurationFile))
                {
                    _configFile = OnionFruitConfigFile.Parser.ParseFrom(fs);
                }

                // read values from config file
                foreach (var entry in _storeEntries.Values)
                {
                    entry.SetFromConfig.Invoke(_configFile);
                }

                foreach (var entry in _storeCollections.Values)
                {
                    entry.SetFromConfig.Invoke(_configFile);
                }
            }
            else
            {
                _configFile = new OnionFruitConfigFile
                {
                    ConfigVersion = ConfigVersion
                };

                // write default values to config file
                foreach (var (key, entry) in _storeEntries)
                {
                    SetValue(key, entry.DefaultValue);
                }

                foreach (var collectionEntry in _storeCollections.Values)
                {
                    collectionEntry.SetDefaultValues.Invoke(_configFile);
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

        private void RegisterOption<T>(OnionFruitSetting key, T defaultValue, string targetPropertyName)
        {
            var observable = RegisterOption(key, defaultValue, out var subject);

            var accessor = typeof(OnionFruitConfigFile).GetProperty(targetPropertyName);
            var accessorClearMethod = typeof(OnionFruitConfigFile).GetMethod($"Clear{targetPropertyName}");

            if (accessor == null)
            {
                throw new ArgumentException("Invalid property name", nameof(targetPropertyName));
            }

            if (defaultValue == null && accessorClearMethod == null)
            {
                throw new ArgumentException("Cannot have a null default value without a clear method", nameof(defaultValue));
            }

            _storeEntries[key] = new SettingsStoreEntry(defaultValue, accessor, accessorClearMethod, c => subject.OnNext((T)accessor.GetValue(c, null)));
            observable.ObserveOn(RxApp.TaskpoolScheduler).Subscribe(value =>
                {
                    if (value == null && _storeEntries[key].ValueClearMethod != null)
                    {
                        _logger.LogDebug("Configuration value {key} cleared", key);
                        _storeEntries[key].ValueClearMethod.Invoke(_configFile, null);
                    }
                    else if (value == null)
                    {
                        _logger.LogDebug("Configuration value {key} reset to default value ('{val}')", key, defaultValue);
                        _storeEntries[key].Accessor.SetValue(_configFile, defaultValue);

                        subject.OnNext(defaultValue);
                    }
                    else
                    {
                        _logger.LogDebug("Configuration value {key} set to '{val}'", key, value);
                        _storeEntries[key].Accessor.SetValue(_configFile, value);
                    }

                    _ = QueueSave();
                })
                .DisposeWith(Subscriptions);
        }

        private void RegisterCollection<T>(OnionFruitSetting key, IEnumerable<T> defaultValue, Func<OnionFruitConfigFile, RepeatedField<T>> rfAccessor)
        {
            var observer = RegisterCollection<T>(key, out var list);
            _storeCollections[key] = new SettingsCollectionEntry(c => list.AddRange(rfAccessor.Invoke(c)), c =>
            {
                var collection = rfAccessor.Invoke(c);

                collection.Clear();
                collection.AddRange(defaultValue);
            });

            observer.ObserveOn(RxApp.TaskpoolScheduler).Subscribe(cs =>
                {
                    var collection = rfAccessor.Invoke(_configFile);

                    using (list.Connect().Bind(out var clonedList).Subscribe())
                    {
                        collection.Clear();
                        collection.AddRange(clonedList.ToList());

                        _logger.LogInformation("Configuration collection {key} updated ({newVals})", key, string.Join(", ", clonedList.AsEnumerable()));
                    }

                    _ = QueueSave();
                })
                .DisposeWith(Subscriptions);
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

        EnableFirewallPortRestrictions,
        AllowedFirewallPorts
    }
}