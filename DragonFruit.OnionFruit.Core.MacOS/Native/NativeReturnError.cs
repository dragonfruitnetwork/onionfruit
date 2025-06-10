// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

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
}