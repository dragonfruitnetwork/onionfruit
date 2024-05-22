// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Configuration
{
    public abstract class SettingsStore<TKey> : IDisposable where TKey : Enum
    {
        protected readonly IDictionary<TKey, object> ConfigStore = new ConcurrentDictionary<TKey, object>();
        protected readonly CompositeDisposable Subscriptions = new();

        /// <summary>
        /// Saves the current configuration to persistent storage
        /// </summary>
        protected abstract Task SaveConfiguration();

        /// <summary>
        /// Loads the configuration from storage
        /// </summary>
        /// <returns></returns>
        protected abstract Task LoadConfiguration();

        /// <summary>
        /// Gets the current value of a specified configuration key
        /// </summary>
        public TValue GetValue<TValue>(TKey key) => GetRootSubject<TValue>(key).Value;

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

            subject.OnNext(value);
        }

        protected BehaviorSubject<TValue> GetRootSubject<TValue>(TKey key)
        {
            if (!ConfigStore.TryGetValue(key, out var subject))
            {
                return null;
            }

            if (subject is BehaviorSubject<TValue> typedSubject)
            {
                return typedSubject;
            }

            throw new InvalidCastException($"Subject is not of the expected type. Expected <{subject.GetType().GetGenericParameterConstraints().Single().Name}>, got <{typeof(TValue).Name}>");
        }

        public void Dispose()
        {
            Subscriptions?.Dispose();
        }
    }
}