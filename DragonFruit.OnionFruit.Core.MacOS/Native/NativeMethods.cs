using System;
using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.MacOS.Native;

internal static partial class NativeMethods
{
    private const string LibraryName = "libonionfruit";

    [LibraryImport(LibraryName, EntryPoint = "createNetworkServiceList")]
    public static partial IntPtr CreateNetworkServiceList(out int count);

    [LibraryImport(LibraryName, EntryPoint = "destroyNetworkServiceList")]
    public static partial void DestroyNetworkServiceList(IntPtr list, int count);

    [LibraryImport(LibraryName, EntryPoint = "createXpcConnection", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr CreateXpcConnection(string serviceName, out int serviceVersion, out long errorCode);

    [LibraryImport(LibraryName, EntryPoint = "destroyXpcConnection")]
    public static partial void DestroyXpcConnection(IntPtr connection);

    [LibraryImport(LibraryName, EntryPoint = "getServiceProxyConfig", StringMarshalling = StringMarshalling.Utf8)]
    public static partial ulong GetServiceProxyConfig(IntPtr xpcConnection, string serviceId, out IntPtr proxyConfigPtr);

    [LibraryImport(LibraryName, EntryPoint = "destroyServiceProxyConfig")]
    public static partial void DestroyProxyConfig(IntPtr proxyConfigPtr);

    [LibraryImport(LibraryName, EntryPoint = "getServiceDnsResolvers", StringMarshalling = StringMarshalling.Utf8)]
    public static partial ulong GetServiceDnsResolvers(IntPtr xpcConnection, string serviceId, out IntPtr resolverListPtr, out int resolverCount);

    [LibraryImport(LibraryName, EntryPoint = "destroyServiceDnsResolvers")]
    public static partial void DestroyDnsResolverList(IntPtr resolverListPtr, int resolverCount);

    [DllImport(LibraryName, EntryPoint = "setServiceDnsResolvers", ExactSpelling = true)]
    public static extern ulong SetServiceDnsResolvers(
        IntPtr xpcConnection,
        [In, MarshalAs(UnmanagedType.LPUTF8Str)] string serviceId,
        [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] resolvers,
        int resolverCount);

    [DllImport(LibraryName, EntryPoint = "setServiceProxyConfig", ExactSpelling = true)]
    public static extern ulong SetServiceProxyConfig(IntPtr xpcConnection, string serviceId, [In, MarshalAs(UnmanagedType.LPStruct)] ServiceProxyConfig config);
}