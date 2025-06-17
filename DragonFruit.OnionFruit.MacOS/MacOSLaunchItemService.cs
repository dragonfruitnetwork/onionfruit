// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using AppServiceSharp;
using AppServiceSharp.Enums;
using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.MacOS
{
    public class MacOSLoginItemService : IStartupLaunchService
    {
        public StartupLaunchState CurrentStartupState => AppService.MainAppService.Status switch
        {
            AppServiceStatus.Enabled => StartupLaunchState.Enabled,
            AppServiceStatus.NotFound => StartupLaunchState.Blocked,
            AppServiceStatus.NotRegistered => StartupLaunchState.Disabled,
            AppServiceStatus.RequiresApproval => StartupLaunchState.Disabled,

            _ => StartupLaunchState.Blocked
        };

        public bool InstanceLaunchedByStartupService => NativeMethods.CurrentProcessLaunchedAsLoginItem();

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
    }
}