// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using AppServiceSharp;
using AppServiceSharp.Enums;
using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.MacOS
{
    public class MacOSLaunchAgentService : IStartupLaunchService
    {
        private readonly AppService _launchAgentManager = AppService.MainAppService;

        private bool? _wasStartedAsLoginItem;

        public StartupLaunchState CurrentStartupState => _launchAgentManager.Status switch
        {
            AppServiceStatus.Enabled => StartupLaunchState.Enabled,
            AppServiceStatus.NotFound => StartupLaunchState.Blocked,
            AppServiceStatus.NotRegistered => StartupLaunchState.Disabled,
            AppServiceStatus.RequiresApproval => StartupLaunchState.Disabled,

            _ => StartupLaunchState.Blocked
        };

        public bool InstanceLaunchedByStartupService => _wasStartedAsLoginItem ??= NativeMethods.CurrentProcessLaunchedAsLoginItem();

        public StartupLaunchState SetStartupState(bool enabled)
        {
            switch (enabled)
            {
                case true when AppService.MainAppService.Status != AppServiceStatus.Enabled:
                    _launchAgentManager.RegisterService();
                    break;

                case false when AppService.MainAppService.Status == AppServiceStatus.Enabled:
                    _launchAgentManager.UnregisterService();
                    break;
            }

            return CurrentStartupState;
        }
    }
}