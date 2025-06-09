// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.MacOS.Native;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct NetworkServiceInfo
{
    [MarshalAs(UnmanagedType.LPUTF8Str)]
    public string ServiceId;

    [MarshalAs(UnmanagedType.LPUTF8Str)]
    public string BsdInterfaceId;

    [MarshalAs(UnmanagedType.LPUTF8Str)]
    public string FriendlyName;
}