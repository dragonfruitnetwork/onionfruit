// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Process
{
    public record TorControlServerMessage(int Status, string StatusMessage, string Reply, string Data);
    public record TorControlServerMessage(int Status, string StatusMessage, string ReplyData);

    /// <summary>
    /// Allows for control of a Tor process via the exposed RPC interface (control port)
    /// </summary>
    public class TorControlPortClient(IPEndPoint endpoint) : IDisposable
    {
        private readonly TcpClient _client = new(endpoint.AddressFamily);
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Connects to the control port as an asynchronous operation
        /// </summary>
        public Task ConnectAsync() => _client.ConnectAsync(endpoint);

        /// <summary>
        /// Disconnects from the control port
        /// </summary>
        public void Disconnect() => _client.Close();

        /// <summary>
        /// Authenticates the control port with the given password as an asynchronous operation
        /// </summary>
        /// <returns>The result of the authentication operation (250 OK or error code)</returns>
        public Task<TorControlServerMessage> AuthenticateAsync(string password)
        {
            return SendAsync($"AUTHENTICATE \"{password}\"");
        }

        /// <summary>
        /// Obtains exclusive access to the control port, sends a message and processes the response as an asynchronous operation
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellation">Optional cancellation token</param>
        /// <returns>A <see cref="TorControlServerMessage"/> describing the response and any data returned with it</returns>
        public async Task<TorControlServerMessage> SendAsync(string message, CancellationToken cancellation = default)
        {
            await _semaphore.WaitAsync(cancellation).ConfigureAwait(false);

            try
            {
                var networkStream = _client.GetStream();

                await using (var writer = new StreamWriter(networkStream, leaveOpen: true))
                {
                    await writer.WriteLineAsync(message).ConfigureAwait(false);
                }

                using var reader = new StreamReader(networkStream, leaveOpen: true);
                var dataLines = new StringBuilder();

                while (true)
                {
                    var responseLine = await reader.ReadLineAsync(cancellation).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(responseLine))
                    {
                        return null;
                    }

                    // see https://github.com/torproject/torspec/blob/8961bb4d83fccb2b987f9899ca83aa430f84ab0c/control-spec.txt#L248-L270 for more info
                    switch (responseLine[3])
                    {
                        case ' ': // statusCode followed by a space indicates the end of the message
                            return new TorControlServerMessage(int.Parse(responseLine[..3]), responseLine[4..], dataLines.ToString());

                        case '-': // statusCode followed by a hyphen indicates a mid-reply line
                            dataLines.AppendLine(responseLine[4..]);
                            break;

                        case '+': // statusCode followed by a plus is a multiline response (keep reading until a line with a single period (.) is encountered)
                            dataLines.AppendLine(responseLine[4..]);

                            while (true)
                            {
                                // https://github.com/torproject/torspec/blob/8961bb4d83fccb2b987f9899ca83aa430f84ab0c/control-spec.txt#L324-L327
                                var dataLine = await reader.ReadLineAsync(cancellation).ConfigureAwait(false);

                                // read in data lines until we hit a line with a single period (.)
                                if (dataLine == ".")
                                {
                                    break;
                                }

                                dataLines.AppendLine(dataLine);
                            }

                            break;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _semaphore?.Dispose();
        }
    }
}