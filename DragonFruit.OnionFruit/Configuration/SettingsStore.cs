// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;

namespace DragonFruit.OnionFruit.Configuration
{
    public abstract class SettingsStore<TKey> : IDisposable where TKey : Enum
    {
        protected readonly IDictionary<TKey, object> ConfigStore = new ConcurrentDictionary<TKey, object>();
        protected readonly CompositeDisposable Subscriptions = new();
        protected readonly BehaviorSubject<bool> IsLoaded = new(false);

        private readonly object _saveLock = new();
        private long _lastSaveValue;

        protected virtual void RegisterSettings()
        {
        }

        protected abstract void LoadConfiguration();
        protected abstract void SaveConfiguration();

        /// <summary>
        /// Gets the current value of a specified configuration key
        /// </summary>
        public TValue GetValue<TValue>(TKey key) => GetRootSubject<TValue>(key).Value;

        /// <summary>
        /// Gets the collection held in the configuration store with the provided key
        /// </summary>
        public SourceList<TValue> GetCollection<TValue>(TKey key) => GetRootCollection<TValue>(key);

        /// <summary>
        /// Gets a source for observing changes to a configuration value
        /// </summary>
        /// <param name="key">The key to get an observable notification stream for</param>
        public IObservable<TValue> GetObservableValue<TValue>(TKey key) => GetRootSubject<TValue>(key);

        /// <summary>
        /// Sets a configuration value, informing observers of the change
        /// </summary>
        public void SetValue<TValue>(TKey key, TValue value)
        {
            var subject = GetRootSubject<TValue>(key);

            if (subject == null)
            {
                throw new ArgumentException($"Key {key} does not exist in the configuration store");
            }

            if (subject.Value?.Equals(value) == true)
            {
                return;
            }

            subject.OnNext(value);
        }

        protected IObservable<TValue> RegisterOption<TValue>(TKey key, TValue defaultValue, out BehaviorSubject<TValue> subject)
        {
            if (ConfigStore.ContainsKey(key))
            {
                throw new ArgumentException($"Key {key} already exists in the configuration store");
            }

            subject = new BehaviorSubject<TValue>(defaultValue);
            ConfigStore.Add(key, subject);

            return subject.Where(_ => IsLoaded.Value).DistinctUntilChanged();
        }

        protected IObservable<IChangeSet<TValue>> RegisterCollection<TValue>(TKey key, out SourceList<TValue> collection)
        {
            if (ConfigStore.ContainsKey(key))
            {
                throw new ArgumentException($"Key {key} already exists in the configuration store");
            }

            collection = new SourceList<TValue>();
            ConfigStore.Add(key, collection);

            return collection.Connect().SkipInitial().Where(_ => IsLoaded.Value);
        }

        protected async Task<Unit> QueueSave()
        {
            var lastSave = Interlocked.Increment(ref _lastSaveValue);

            await Task.Delay(250).ConfigureAwait(false);

            if (lastSave == Interlocked.Read(ref _lastSaveValue))
            {
                lock (_saveLock)
                {
                    SaveConfiguration();
                }
            }

            return Unit.Default;
        }

        private BehaviorSubject<TValue> GetRootSubject<TValue>(TKey key) => GetRoot<BehaviorSubject<TValue>>(key);
        private SourceList<TValue> GetRootCollection<TValue>(TKey key) => GetRoot<SourceList<TValue>>(key);

        private TRoot GetRoot<TRoot>(TKey key) where TRoot : class
        {
            if (!ConfigStore.TryGetValue(key, out var subject))
            {
                return null;
            }

            if (subject is TRoot typedSubject)
            {
                return typedSubject;
            }

            throw new InvalidCastException($"Subject is not of the expected type. Expected <{subject.GetType().GetGenericParameterConstraints().Single().Name}>, got <{typeof(TRoot).Name}>");
        }

        public void Dispose()
        {
            Subscriptions?.Dispose();
        }
    }
}