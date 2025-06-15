// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    public class MacOSExecutableLocator : ExecutableLocator
    {
        public override string ExecutableSuffix => string.Empty;

        public override IEnumerable<string> SupportedPlatforms => RuntimeInformation.OSArchitecture switch
        {
            // arm64 can fall back to x64 using Rosetta 2
            Architecture.Arm64 => ["osx-arm64", "osx-x64"],

            // x64... not so much
            Architecture.X64 => ["osx-x64"],

            // unsupported architectures
            _ => []
        };
    }
}