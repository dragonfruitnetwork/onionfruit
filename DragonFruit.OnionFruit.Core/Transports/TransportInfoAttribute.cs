// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Diagnostics.CodeAnalysis;

namespace DragonFruit.OnionFruit.Core.Transports
{
    [AttributeUsage(AttributeTargets.Field)]
    public class TransportInfoAttribute : Attribute
    {
        /// <summary>
        /// Creates a <see cref="TransportInfoAttribute"/> with the <see cref="Id"/> and <see cref="TransportEngine"/> set to the same value
        /// </summary>
        public TransportInfoAttribute(string id)
            : this(id, id)
        {
        }

        public TransportInfoAttribute(string id, string transportEngine)
        {
            Id = id;
            TransportEngine = transportEngine;
        }

        public string Id { get; }
        public string TransportEngine { get; }

        [MaybeNull]
        public string DefaultBridgeKey { get; set; }
    }
}