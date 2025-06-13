// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Runtime.InteropServices;
using AppServiceSharp;
using AppServiceSharp.Enums;
using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.MacOS
{
    public partial class MacOSLaunchItemService : IStartupLaunchService
    {
        public StartupLaunchState CurrentStartupState => StartupLaunchState.Blocked;

        public bool InstanceLaunchedByStartupService => false;

        public StartupLaunchState SetStartupState(bool enabled)
        {
            switch (enabled)
            {
                case true when AppService.MainAppService.Status != AppServiceStatus.Enabled:
                    AppService.MainAppService.RegisterService();
                    break;

                case false when AppService.MainAppService.Status == AppServiceStatus.Enabled:
                    AppService.MainAppService.UnregisterService();
                    break;
            }

            return CurrentStartupState;
        }

        private static partial class NativeMethods
        {
            [LibraryImport("libSystem.dylib", EntryPoint = "getppid")]
            public static partial uint GetParentProcessId();
        }
    }
}