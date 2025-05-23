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

        public ElevationStatus CheckElevationStatus()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var isAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);

            return isAdmin ? ElevationStatus.Elevated : ElevationStatus.CanElevate;
        }

        public bool RelaunchProcess(bool elevated)
        {
            var file = CurrentProcess.MainModule?.FileName ?? Assembly.GetEntryAssembly()?.Location;

            if (string.IsNullOrEmpty(file))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo(file)
            {
                Arguments = string.Concat(' ', Environment.GetCommandLineArgs()),
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true,
                Verb = elevated ? "runas" : null
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