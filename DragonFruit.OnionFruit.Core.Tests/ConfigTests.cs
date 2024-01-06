// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Core.Config;
using Xunit;

namespace DragonFruit.OnionFruit.Core.Tests
{
    public class ConfigTests
    {
        [Fact]
        public async Task TestConfigWriter()
        {
            using var stream = new MemoryStream(6000);
            await using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                var config = new ClientConfig
                {
                    ClientOnly = true,
                    EnableLogScrubbing = false,
                    ExternalConnectionKeepAlive = TimeSpan.FromMinutes(5),
                    Endpoints =
                    [
                        new IPEndPoint(IPAddress.Loopback, 9050),
                        new IPEndPoint(IPAddress.IPv6Loopback, 9999)
                    ]
                };

                await config.WriteAsync(writer);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);

            var contents = await reader.ReadToEndAsync();
            var lines = contents.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Contains("ClientOnly 1", lines);
            Assert.Contains("SafeLogging 1", lines);
            Assert.Contains("KeepAlivePeriod 300", lines);

            Assert.Contains("SocksPort [::1]:9999", lines);
            Assert.Contains("SocksPort 127.0.0.1:9050", lines);
        }
    }
}