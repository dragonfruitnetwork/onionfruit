// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using AppServiceSharp;
using AppServiceSharp.Enums;
using DragonFruit.OnionFruit.MacOS.ViewModels;
using DragonFruit.OnionFruit.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DragonFruit.OnionFruit.MacOS
{
    public class MacOSPreflightCheck([FromKeyedServices("DaemonAppService")] AppService service) : ISessionPreFlightCheck
    {
        public string PerformPreFlightCheck()
        {
            if (service.Status != AppServiceStatus.Enabled)
            {
                return MacOSSettingsWindowViewModel.ServiceManagementTabId;
            }

            return null;
        }
    }
}