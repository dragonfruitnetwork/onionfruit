// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Runtime.InteropServices;

// ReSharper disable once RedundantUsingDirective
using CoreNative = DragonFruit.OnionFruit.Core.MacOS.Native;

namespace DragonFruit.OnionFruit.MacOS
{
    internal static partial class NativeMethods
    {
        [LibraryImport(CoreNative::NativeMethods.LibraryName, EntryPoint = "showMessageBox", StringMarshalling = StringMarshalling.Utf8)]
        public static partial void ShowMessageBox(string title, string message, string buttonText);

        [return: MarshalAs(UnmanagedType.U1)]
        [LibraryImport(CoreNative::NativeMethods.LibraryName, EntryPoint = "currentProcessLaunchedAsLoginItem")]
        public static partial bool CurrentProcessLaunchedAsLoginItem();
    }
}