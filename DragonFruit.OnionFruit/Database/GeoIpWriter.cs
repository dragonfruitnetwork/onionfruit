// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;

namespace DragonFruit.OnionFruit.Database
{
    public class GeoIpWriter(TextWriter writer) : IDisposable
    {
        private readonly byte[] _addressBuffer = ArrayPool<byte>.Shared.Rent(16);
        private bool _disposed;

        private static FileStreamOptions FileStreamOptions => new()
        {
            Access = FileAccess.Write,
            Share = FileShare.None,
            Mode = FileMode.Create,
            BufferSize = 8192
        };

        public GeoIpWriter(string outputPath) : this(new StreamWriter(outputPath, Encoding.ASCII, FileStreamOptions))
        {
        }

        /// <summary>
        /// Writes a header to the file
        /// </summary>
        /// <returns></returns>
        public Task WriteHeader(OnionDb database)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return writer.WriteLineAsync($"# OnionFruit GeoIP v{database.DbVersion}.\n# Data contained inside this file is licensed under {database.GeoLicense} and {database.TorLicense}");
        }

        /// <summary>
        /// Writes the provided ranges to the output buffer
        /// </summary>
        /// <param name="countryCode">The country code the ranges are associated with</param>
        /// <param name="ranges">The IP address ranges to write</param>
        public async Task WriteRanges(string countryCode, IEnumerable<AddressRange> ranges)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            foreach (var addressBlock in ranges)
            {
                var startAddress = CreateAddressFromByteString(addressBlock.Start);
                var endAddress = CreateAddressFromByteString(addressBlock.End);

                Debug.Assert(startAddress.AddressFamily == endAddress.AddressFamily);

                if (startAddress.AddressFamily == AddressFamily.InterNetwork)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    var startValue = (uint)IPAddress.HostToNetworkOrder((int)startAddress.Address);
                    var endValue = (uint)IPAddress.HostToNetworkOrder((int)endAddress.Address);
#pragma warning restore CS0618 // Type or member is obsolete

                    // IPv4 need their addresses as numbers, not addresses with dots
                    await writer.WriteLineAsync($"{startValue},{endValue},{countryCode}").ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync($"{startAddress},{endAddress},{countryCode}").ConfigureAwait(false);
                }
            }
        }

        private IPAddress CreateAddressFromByteString(ByteString addressBytes, bool convertMappedAddresses = true)
        {
            addressBytes.CopyTo(_addressBuffer, 0);
            var address = new IPAddress(_addressBuffer.AsSpan(0, addressBytes.Length));

            return address.IsIPv4MappedToIPv6 && convertMappedAddresses ? address.MapToIPv4() : address;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            writer?.Dispose();
            ArrayPool<byte>.Shared.Return(_addressBuffer);
        }
    }
}