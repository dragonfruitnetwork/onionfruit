// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Data;
using Velopack.Sources;

namespace DragonFruit.OnionFruit.Updater
{
    /// <summary>
    /// A replacement to the <see cref="HttpClientFileDownloader"/>, leveraging the already-used <see cref="ApiClient"/> to perform network operations.
    /// </summary>
    public class VelopackFileDownloader(ApiClient client) : IFileDownloader
    {
        public async Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string> headers, double timeout, CancellationToken cancelToken)
        {
            using var fileRequest = PrepareRequest(url, headers);
            using var fileDestinationStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);

            using var timeoutCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            using var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCancellationSource.Token);

            var progressReporter = new Progress<(long, long?)>(t => ReportProgress(t, progress));
            var responseCode = await client.PerformDownload(fileRequest, fileDestinationStream, progressReporter, cancellationToken: linkedCancellationSource.Token).ConfigureAwait(false);

            if (responseCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Failed to download file", null, responseCode);
            }

            return;

            static void ReportProgress((long Recieved, long? Total) bytes, Action<int> progressEvent)
            {
                if (bytes.Total.HasValue)
                {
                    var progress = (int)(bytes.Recieved / (double)bytes.Total.Value * 100);
                    progressEvent.Invoke(progress);
                }
            }
        }

        public async Task<byte[]> DownloadBytes(string url, IDictionary<string, string> headers, double timeout)
        {
            using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            using var response = await client.PerformAsync(PrepareRequest(url, headers), cancellationToken.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken.Token).ConfigureAwait(false);
        }

        public async Task<string> DownloadString(string url, IDictionary<string, string> headers, double timeout)
        {
            using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            using var response = await client.PerformAsync(PrepareRequest(url, headers), cancellationToken.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken.Token).ConfigureAwait(false);
        }

        private static HttpRequestMessage PrepareRequest(string url, IDictionary<string, string> headers)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (headers?.Count > 0)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return request;
        }
    }
}