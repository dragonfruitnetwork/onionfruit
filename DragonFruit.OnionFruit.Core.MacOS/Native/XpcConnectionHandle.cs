// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using Microsoft.Win32.SafeHandles;

namespace DragonFruit.OnionFruit.Core.MacOS.Native
{
    internal class XpcConnectionHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
    {
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.DestroyXpcConnection(this);
                handle = IntPtr.Zero;
                return true;
            }

            return false;
        }
    }
}