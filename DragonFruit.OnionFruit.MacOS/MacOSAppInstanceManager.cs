// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.MacOS
{
    public class MacOSAppInstanceManager : IProcessElevator
    {
        public ElevationStatus CheckElevationStatus()
        {
            return ElevationStatus.CannotElevate;
        }

        public bool RelaunchProcess(bool elevated) => false;
    }
}