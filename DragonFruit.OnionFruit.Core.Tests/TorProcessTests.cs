// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Core.Config;
using DragonFruit.OnionFruit.Core.Network;
using DragonFruit.OnionFruit.Core.Windows;
using Xunit;
using Xunit.Abstractions;

namespace DragonFruit.OnionFruit.Core.Tests
{
    public class TorProcessTests(ITestOutputHelper output)
    {
        [Fact]
        public async Task TestTorProcessMonitoring()
        {
            var client = new ClientConfig
            {
                ClientOnly = true,
                EnableLogScrubbing = false,
                Endpoints =
                [
                    new IPEndPoint(IPAddress.Loopback, PortScanner.GetClosestFreePort(9050) ?? 45221)
                ]
            };

            var processLocations = new WindowsExecutableLocator(null).LocateExecutableInstancesOf("tor");
            Assert.True(processLocations.Any());

            var process = new TorProcess(processLocations.First(), output.BuildLoggerFor<TorProcess>());
            var processWaiter = new TaskCompletionSource();

            process.BootstrapProgressChanged += (_, pc) => output.WriteLine("Bootstrapping client: {0}%", pc);
            process.ProcessStateChanged += (_, state) =>
            {
                output.WriteLine("Process state changed to {0}", state);

                switch (state)
                {
                    case TorProcess.State.Running:
                        processWaiter.TrySetResult();
                        break;

                    case TorProcess.State.Stopped or TorProcess.State.Blocked or TorProcess.State.Killed:
                        processWaiter.TrySetException(new Exception("Failed to start tor process"));
                        break;
                }
            };

            await process.StartProcessWithConfig(client);
            await processWaiter.Task.WaitAsync(TimeSpan.FromSeconds(60));

            await Task.Delay(1000);
            await process.StopProcessAsync();
        }
    }
}