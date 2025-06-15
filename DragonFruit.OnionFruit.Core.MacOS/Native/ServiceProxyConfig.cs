using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.MacOS.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct ServiceProxyConfig
    {
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string HttpProxyHost;

        [MarshalAs(UnmanagedType.U2)]
        public ushort HttpProxyPort;

        [MarshalAs(UnmanagedType.U1)]
        public bool HttpProxyEnabled;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string HttpsProxyHost;

        [MarshalAs(UnmanagedType.U2)]
        public ushort HttpsProxyPort;

        [MarshalAs(UnmanagedType.U1)]
        public bool HttpsProxyEnabled;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string SocksProxyHost;

        [MarshalAs(UnmanagedType.U2)]
        public ushort SocksProxyPort;

        [MarshalAs(UnmanagedType.U1)]
        public bool SocksProxyEnabled;

        [MarshalAs(UnmanagedType.U1)]
        public bool AutoDiscoveryEnabled;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string AutoConfigurationUrl;
    }
}