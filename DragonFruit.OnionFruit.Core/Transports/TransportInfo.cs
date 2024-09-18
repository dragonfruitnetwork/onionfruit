// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Diagnostics.CodeAnalysis;

namespace DragonFruit.OnionFruit.Core.Transports
{
    [AttributeUsage(AttributeTargets.Field)]
    public class TransportInfo : Attribute
    {
        /// <summary>
        /// Creates a <see cref="TransportInfo"/> with the <see cref="Id"/> and <see cref="TransportEngine"/> set to the same value
        /// </summary>
        public TransportInfo(string id)
            : this(id, id)
        {
        }

        public TransportInfo(string id, string transportEngine)
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