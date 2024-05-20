// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public class WindowsExecutableLocator(string envVarOverride) : ExecutableLocator
    {
        public override string ExecutableSuffix => ".exe";

        // https://learn.microsoft.com/en-us/dotnet/core/rid-catalog#windows-rids
        public override IEnumerable<string> SupportedPlatforms => RuntimeInformation.OSArchitecture switch
        {
            // windows on arm64 supports x64 binaries via translation layer
            Architecture.Arm64 => ["win-arm64", "win-x64"],

            // x64 supports x86 via WindowsOnWindows
            Architecture.X64 => ["win-x64", "win-x86"],

            // x86 is on its own
            Architecture.X86 => ["win-x86"],

            // no other platforms supported
            _ => []
        };

        public override IEnumerable<string> LocateExecutableInstancesOf(string executableName)
        {
            if (string.IsNullOrEmpty(envVarOverride))
            {
                return base.LocateExecutableInstancesOf(executableName);
            }

            // check HOME variable if set, otherwise standard locations
            var homePath = Environment.GetEnvironmentVariable(envVarOverride);
            var entries = base.LocateExecutableInstancesOf(executableName);

            if (!string.IsNullOrEmpty(homePath))
            {
                var homeTarget = Path.Combine(homePath, executableName + ExecutableSuffix);

                if (File.Exists(homeTarget))
                {
                    return entries.Prepend(homeTarget);
                }
            }

            return entries;
        }
    }
}