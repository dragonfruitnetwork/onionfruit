// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using DragonFruit.OnionFruit.Services;
using DragonFruit.OnionFruit.Updater;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Windows
{
    public partial class StartupLaunchService : IStartupLaunchService, IDisposable
    {
        private const string StartupAppName = "OnionFruit";
        private const string StartupAppArgs = "--autostart";
        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const RegistryRights RequiredRights = RegistryRights.ReadKey | RegistryRights.WriteKey | RegistryRights.SetValue | RegistryRights.Delete;

        private readonly RegistryKey _startupCollectionKey;
        private readonly IOnionFruitUpdater _updater;

        public StartupLaunchService(IOnionFruitUpdater updater, string[] launchArgs)
        {
            _updater = updater;

            try
            {
                _startupCollectionKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRights);
            }
            catch (SecurityException)
            {
                _startupCollectionKey = null;
            }

            InstanceLaunchedByStartupService = launchArgs?.Any(x => x.Equals(StartupAppArgs, StringComparison.OrdinalIgnoreCase)) == true;
        }

        public bool InstanceLaunchedByStartupService { get; }

        public StartupLaunchState CurrentStartupState => _startupCollectionKey == null ? StartupLaunchState.Blocked : GetStartupStateImpl();

        public StartupLaunchState SetStartupState(bool enabled)
        {
            if (_startupCollectionKey == null)
            {
                return StartupLaunchState.Blocked;
            }

            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (executablePath == null)
            {
                return StartupLaunchState.Disabled;
            }

            if (enabled)
            {
                _startupCollectionKey.SetValue(StartupAppName, $@"""{Path.GetFullPath(executablePath)}"" {StartupAppArgs}");
                return StartupLaunchState.Enabled;
            }

            _startupCollectionKey.DeleteValue(StartupAppName, false);
            return StartupLaunchState.Disabled;
        }

        private StartupLaunchState GetStartupStateImpl()
        {
#if !DEBUG
            if (_updater?.IsInstalled != true)
            {
                return StartupLaunchState.Blocked;
            }
#endif

            var startupArgs = _startupCollectionKey.GetValue(StartupAppName);
            if (startupArgs is not string startupString)
            {
                return StartupLaunchState.Disabled;
            }

            // validate input
            var startupLaunchCommand = LaunchCommandRegex().Match(startupString);
            if (!startupLaunchCommand.Success || startupLaunchCommand.Groups["executable"].Length == 0)
            {
                return StartupLaunchState.EnabledInvalid;
            }

            // check for correct executable path (executable must match)
            var executablePath = Path.GetFullPath(startupLaunchCommand.Groups["executable"].Value);
            if (executablePath != Process.GetCurrentProcess().MainModule?.FileName)
            {
                return StartupLaunchState.EnabledInvalid;
            }

            // check for startup args
            if (!startupLaunchCommand.Groups["args"].Success || !startupLaunchCommand.Groups["args"].Value.Equals(StartupAppArgs, StringComparison.OrdinalIgnoreCase))
            {
                return StartupLaunchState.EnabledInvalid;
            }

            return StartupLaunchState.Enabled;
        }

        public void Dispose()
        {
            _startupCollectionKey?.Dispose();
        }

        [GeneratedRegex(@"^""(?<executable>.*)""(?: (?<args>.+))?$", RegexOptions.Singleline | RegexOptions.CultureInvariant, "en-US")]
        private partial Regex LaunchCommandRegex();
    }
}