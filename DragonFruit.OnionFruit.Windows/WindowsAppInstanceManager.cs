// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.Windows
{
    public class WindowsAppInstanceManager : IProcessElevator
    {
        public Process CurrentProcess { get; } = Process.GetCurrentProcess();

        /// <summary>
        /// Stores the <see cref="Process"/> that will replace the current one.
        /// </summary>
        public Process ReplacementProcess { get; private set; }

        public bool CheckElevationStatus()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public bool ElevatePermissions()
        {
            var file = CurrentProcess.MainModule?.FileName ?? Assembly.GetEntryAssembly()?.Location;

            if (string.IsNullOrEmpty(file))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo(file)
            {
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                ReplacementProcess = Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}