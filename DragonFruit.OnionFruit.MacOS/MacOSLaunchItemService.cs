// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Linq;
using AppServiceSharp;
using AppServiceSharp.Enums;
using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.MacOS
{
    public class MacOSLaunchAgentService : IStartupLaunchService
    {
        private const string LaunchAgentPlist = "autostart.plist";
        private const string AutostartArgument = "--launchd";

        private readonly AppService _launchAgentManager;
        private readonly string[] _instanceLaunchArgs;

        public MacOSLaunchAgentService(string[] instanceLaunchArgs)
        {
            _instanceLaunchArgs = instanceLaunchArgs;
            _launchAgentManager = AppService.AgentServiceWithPlistName(LaunchAgentPlist);
        }

        public StartupLaunchState CurrentStartupState => _launchAgentManager.Status switch
        {
            AppServiceStatus.Enabled => StartupLaunchState.Enabled,
            AppServiceStatus.NotFound => StartupLaunchState.Blocked,
            AppServiceStatus.NotRegistered => StartupLaunchState.Disabled,
            AppServiceStatus.RequiresApproval => StartupLaunchState.EnabledInvalid,

            _ => StartupLaunchState.Blocked
        };

        public bool InstanceLaunchedByStartupService => _instanceLaunchArgs.Contains(AutostartArgument, StringComparer.OrdinalIgnoreCase);

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