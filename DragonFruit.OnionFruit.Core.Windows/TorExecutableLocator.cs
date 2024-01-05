// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public class TorExecutableLocator : ExecutableLocator
    {
        protected override string ExecutableSuffix => ".exe";

        // https://learn.microsoft.com/en-us/dotnet/core/rid-catalog#windows-rids
        protected override IEnumerable<string> SupportedPlatforms => RuntimeInformation.OSArchitecture switch
        {
            // windows on arm64 supports x64 binaries via translation layer
            Architecture.Arm64 => ["win-arm64", "win-x64"],

            // x64 supports x86 via WindowsOnWindows
            Architecture.X64 => ["win-x64", "win-x86"],

            // x86 is on its own
            Architecture.X86 => ["win-x86"],

            // no other platforms supported
            _ => Enumerable.Empty<string>()
        };

        public override IEnumerable<string> LocateExecutableInstancesOf(string executableName)
        {
            // check TOR_HOME variable if set, otherwise standard locations
            var torHome = Environment.GetEnvironmentVariable("TOR_HOME");

            return string.IsNullOrEmpty(torHome)
                ? Enumerable.Empty<string>().Append(torHome)
                : base.LocateExecutableInstancesOf(executableName);
        }
    }
}