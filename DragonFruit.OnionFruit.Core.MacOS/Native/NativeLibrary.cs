// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.MacOS.Native
{
    internal static class NativeLibrary
    {
        internal const int CurrentNativeApiVersion = 1;
        private const string NativeApiLibraryName = "libonionfruit.dylib";

        [DllImport(NativeApiLibraryName, EntryPoint = "getApiVersion")]
        public static extern ushort GetApiVersion();

        [DllImport(NativeApiLibraryName, EntryPoint = "openLoginItemsSettings")]
        public static extern void OpenLoginItemsSettings();

        [DllImport(NativeApiLibraryName, EntryPoint = "createManager")]
        public static extern IntPtr CreateServiceManager();

        [return: MarshalAs(UnmanagedType.I4)]
        [DllImport(NativeApiLibraryName, EntryPoint = "getInstallState")]
        public static extern ServiceInstallState GetServiceInstallState(IntPtr handle);

        [DllImport(NativeApiLibraryName, EntryPoint = "performRegistration")]
        public static extern int PerformServiceInstallation(IntPtr handle, [MarshalAs(UnmanagedType.I4)] out ServiceInstallState installState);

        [DllImport(NativeApiLibraryName, EntryPoint = "performRemoval")]
        public static extern int PerformServiceRemoval(IntPtr handle, [MarshalAs(UnmanagedType.I4)] out ServiceInstallState installState);

        [DllImport(NativeApiLibraryName, EntryPoint = "closeManager")]
        public static extern void CleanupServiceManager(IntPtr handle);
    }
}