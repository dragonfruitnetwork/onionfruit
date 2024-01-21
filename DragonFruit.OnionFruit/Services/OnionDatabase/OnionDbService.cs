// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Data;

namespace DragonFruit.OnionFruit.Services.OnionDatabase
{
    /// <summary>
    /// Hosted service responsible for managing the onion.db and geoip files
    /// </summary>
    public class OnionDbService(ApiClient client)
    {
        // this will need to be moved to a central location when adding in settings, etc.
        private static string DatabasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonFruit Network", "OnionFruit", "onion.db");

        static OnionDbService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
        }

        private CancellationTokenSource _cancellation;

        private async Task CheckDatabase()
        {
            DateTimeOffset? fileLastModified = null;

            // check for an onion.db file
            if (File.Exists(DatabasePath))
            {
                fileLastModified = File.GetLastWriteTimeUtc(DatabasePath);
            }

            OnionDb currentDb;

            using (var databaseStream = new FileStream(DatabasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                // redownload if file has 0-length or is older than 12 hours
                if (databaseStream.Length == 0 || DateTimeOffset.UtcNow - fileLastModified > TimeSpan.FromHours(12))
                {
                    // todo add progress tracking, handle errors from PerformDownload

                    var onionDbRequest = new OnionDbDownloadRequest(fileLastModified);
                    await client.PerformDownload(onionDbRequest, databaseStream, null, true, false, _cancellation.Token).ConfigureAwait(false);
                }

                databaseStream.Seek(0, SeekOrigin.Begin);
                currentDb = OnionDb.Parser.ParseFrom(databaseStream);
            }

            // check if geoip files exist and replace them if needed
        }
    }
}