// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Runtime.InteropServices;
using DragonFruit.OnionFruit.Core.MacOS.NativeStructs;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    /// <summary>
    /// Represents a macOS network service, used to wrap a network interface and its associated configurations.
    /// </summary>
    /// <param name="ServiceId">The service identifier, used to modify configuration details</param>
    /// <param name="BsdInterfaceId">The underlying BSD network interface identifier</param>
    /// <param name="ServiceName">The friendly name, displayed to users in the System Settings application.</param>
    public record NetworkService(string ServiceId, string BsdInterfaceId, string ServiceName)
    {
        /// <summary>
        /// Gets the currently configured network services on the system.
        /// </summary>
        /// <returns>An array of <see cref="NetworkService"/> items</returns>
        public static NetworkService[] GetNetworkServices()
        {
            var nativeServiceList = NativeMethods.CreateNetworkServiceList(out var count);
            if (count == 0)
            {
                // nothing gets allocated if there are no services
                return [];
            }

            try
            {
                var servicePtr = nativeServiceList;
                var serviceList = new NetworkService[count];
                var serviceSize = Marshal.SizeOf<NetworkServiceInfo>();

                for (var i = 0; i < count; i++)
                {
                    var service = Marshal.PtrToStructure<NetworkServiceInfo>(servicePtr);
                    serviceList[i] = new NetworkService(service.ServiceId, service.BsdInterfaceId, service.FriendlyName);

                    servicePtr += serviceSize;
                }

                return serviceList;
            }
            finally
            {
                NativeMethods.DestroyNetworkServiceList(nativeServiceList, count);
            }
        }
    }
}