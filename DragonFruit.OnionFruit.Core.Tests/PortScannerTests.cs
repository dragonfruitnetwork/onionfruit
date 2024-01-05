using System.Net;
using System.Net.Sockets;
using DragonFruit.OnionFruit.Core.Network;
using Xunit;

namespace DragonFruit.OnionFruit.Core.Tests
{
    public class PortScannerTests
    {
        [Fact]
        public void TestPortScanner()
        {
            const int target = 9999;

            var port = PortScanner.GetClosestFreePort(target);
            Assert.NotNull(port);

            // open a tcpserver to occupy the port
            using var server = new TcpListener(IPAddress.Loopback, port.Value);
            server.Start();

            // next port should not be the same as the one we just occupied
            var newPort = PortScanner.GetClosestFreePort(target);
            Assert.NotEqual(port, newPort);

            server.Stop();
        }
    }
}