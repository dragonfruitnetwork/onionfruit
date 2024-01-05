// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO;
using System.Linq;
using DragonFruit.OnionFruit.Core.Windows;
using Xunit;

namespace DragonFruit.OnionFruit.Core.Tests
{
    public class ExecutableLocatorTests
    {
        [Fact]
        public void TestExecutableLocator()
        {
            // testing without an override = use local assets (which exist because native package is imported)
            // using the override (where the dir doesn't exist) should return the same result
            var tor = new WindowsExecutableLocator(null).LocateExecutableInstancesOf("tor");
            var torAsWell = new WindowsExecutableLocator("SOME_RANDOM_ENVVAR").LocateExecutableInstancesOf("tor");

            Assert.NotNull(tor);
            Assert.NotNull(torAsWell);

            // because the dir doesn't exist, the result should be the same
            Assert.True(tor.SequenceEqual(torAsWell));
            Assert.All(tor, x => File.Exists(x));
        }

        [Fact]
        public void TestExecutableLocatorWithEnvOverride()
        {
            var tempPath = Path.GetTempPath();
            var locator = new WindowsExecutableLocator("TEST_HOME_TMP");

            Environment.SetEnvironmentVariable("TEST_HOME_TMP", tempPath, EnvironmentVariableTarget.Process);

            // create a 0-byte file with the correct name and see if it works
            var dummyFilePath = Path.Combine(tempPath, $"test-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" + locator.ExecutableSuffix);
            using var dummyFile = new FileStream(dummyFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 128, FileOptions.DeleteOnClose);

            // write something to make it look like an actual file
            dummyFile.WriteByte(0x02);
            dummyFile.Flush();

            var testLocation = locator.LocateExecutableInstancesOf(Path.GetFileNameWithoutExtension(dummyFilePath));
            Assert.True(testLocation.SingleOrDefault() == dummyFilePath);
        }

        [Fact]
        public void TestExecutableLocatorWithEnvOverrideAndNormalFile()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            Environment.SetEnvironmentVariable("TOR_HOME_TEMP", tempPath, EnvironmentVariableTarget.Process);
            Directory.CreateDirectory(tempPath);

            var locator = new WindowsExecutableLocator("TOR_HOME_TEMP");
            var dummyFilePath = Path.Combine(tempPath, "tor" + locator.ExecutableSuffix);

            try
            {
                // create a 0-byte file with the correct name and see if it works
                using (var dummyFile = new FileStream(dummyFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 128, FileOptions.DeleteOnClose))
                {
                    // write something to make it look like an actual file
                    dummyFile.WriteByte(0x02);
                    dummyFile.Flush();

                    var testLocations = locator.LocateExecutableInstancesOf("tor");
                    var defaultLocations = new WindowsExecutableLocator(null).LocateExecutableInstancesOf("tor");

                    // the test location should occur first, then the default ones...
                    Assert.True(testLocations.Skip(1).SequenceEqual(defaultLocations));
                }
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }
    }
}