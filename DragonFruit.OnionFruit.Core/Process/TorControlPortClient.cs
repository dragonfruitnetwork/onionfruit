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

    /// <summary>
    /// Allows for control of a tor process via the control port
    /// </summary>
    public class TorControlPortClient(IPEndPoint endpoint) : IDisposable
    {
        private readonly TcpClient _client = new(endpoint);
        private readonly SemaphoreSlim _semaphore = new(1, 1);

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

                var replyLines = new StringBuilder();
                var dataLines = new StringBuilder();

                using var reader = new StreamReader(networkStream, leaveOpen: true);

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
                            return new TorControlServerMessage(int.Parse(responseLine[..3]), responseLine[4..], replyLines.ToString(), dataLines.ToString());

                        case '-': // statusCode followed by a hyphen indicates a mid-reply line
                            replyLines.AppendLine(responseLine[4..]);
                            break;

                        case '+': // statusCode followed by a plus indicates the lines preceding this one are data lines
                            replyLines.AppendLine(responseLine[4..]);

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