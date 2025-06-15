using System;
using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.MacOS.Native
{
    internal enum NativeStatus
    {
        Ok = 0,
        NetworkServiceNotFound = 1,
        NetworkProtocolNotSupported = 2,
        ConfigurationUpdateFailed = 3,
        XpcConnectionFailed = 4,
        XpcRequestTimeout = 5,
        XpcVersionMismatch = 6,
    }

    internal static partial class NativeMethods
    {
        private const string LibraryName = "libonionfruit";

        [LibraryImport(LibraryName, EntryPoint = "createNetworkServiceList")]
        public static partial IntPtr CreateNetworkServiceList(out int count);

        [LibraryImport(LibraryName, EntryPoint = "destroyNetworkServiceList")]
        public static partial void DestroyNetworkServiceList(IntPtr list, int count);

        [LibraryImport(LibraryName, EntryPoint = "createXpcConnection", StringMarshalling = StringMarshalling.Utf8)]
        public static partial NativeStatus CreateXpcConnection(string serviceName, out XpcConnectionHandle connectionRef, out int serverVersion);

        [LibraryImport(LibraryName, EntryPoint = "destroyXpcConnection")]
        public static partial void DestroyXpcConnection(XpcConnectionHandle connection);

        [LibraryImport(LibraryName, EntryPoint = "getServiceProxyConfig", StringMarshalling = StringMarshalling.Utf8)]
        public static partial NativeStatus GetServiceProxyConfig(XpcConnectionHandle xpcConnection, string serviceId, out IntPtr proxyConfigPtr);

        [LibraryImport(LibraryName, EntryPoint = "destroyServiceProxyConfig")]
        public static partial void DestroyProxyConfig(IntPtr proxyConfigPtr);

        [LibraryImport(LibraryName, EntryPoint = "getServiceDnsResolvers", StringMarshalling = StringMarshalling.Utf8)]
        public static partial NativeStatus GetServiceDnsResolvers(XpcConnectionHandle xpcConnection, string serviceId, out IntPtr resolverListPtr, out int resolverCount);

        [LibraryImport(LibraryName, EntryPoint = "destroyServiceDnsResolvers")]
        public static partial void DestroyDnsResolverList(IntPtr resolverListPtr);

        [DllImport(LibraryName, EntryPoint = "setServiceDnsResolvers", ExactSpelling = true)]
        public static extern NativeStatus SetServiceDnsResolvers(
            XpcConnectionHandle xpcConnection,
            [In, MarshalAs(UnmanagedType.LPUTF8Str)] string serviceId,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] resolvers,
            int resolverCount);

        [DllImport(LibraryName, EntryPoint = "setServiceProxyConfig", ExactSpelling = true)]
        public static extern NativeStatus SetServiceProxyConfig(XpcConnectionHandle xpcConnection, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string serviceId, [In] ServiceProxyConfig configPtr);

        [LibraryImport(LibraryName, EntryPoint = "showMessageBox", StringMarshalling = StringMarshalling.Utf8)]
        public static partial void ShowMessageBox(string title, string message, string buttonText);
    }
}