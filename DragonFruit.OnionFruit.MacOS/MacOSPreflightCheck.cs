// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using DragonFruit.OnionFruit.Core.MacOS;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.MacOS.ViewModels;
using DragonFruit.OnionFruit.Models;

namespace DragonFruit.OnionFruit.MacOS
{
    public class MacOSPreflightCheck(MacOSNetworkServiceManager networkManager) : ISessionPreFlightCheck
    {
        public PreflightCheckFailure PerformPreFlightCheck()
        {
            // check service is enabled
            if (networkManager.ProxyState != NetworkComponentState.Available)
            {
                return new PreflightCheckFailure("The service responsible for changing settings is not enabled. Please enable it in the settings.", MacOSSettingsWindowViewModel.ServiceManagementTabId);
            }

            // check daemon is contactable
            if (!networkManager.CheckDaemonConnection())
            {
                MacOSMessageBox.Show("Service Error", "OnionFruit was unable to contact a service used to change settings. Try restarting your computer and try again.\n\nIf the problem persists, please report this issue on GitHub.");
                return new PreflightCheckFailure("The onionfruitd service timed out trying to establish a connection.");
            }

            return null;
        }
    }
}