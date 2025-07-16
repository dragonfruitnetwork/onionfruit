// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using DragonFruit.Data;
using DragonFruit.Data.Requests;

namespace DragonFruit.OnionFruit.Database
{
    /// <summary>
    /// Represents a request to download the latest onion.db file
    /// </summary>
    public partial class OnionDbDownloadRequest(DateTimeOffset? previousDatabaseVersion, bool useBrotli) : ApiRequest
    {
        public override string RequestPath => "https://onionfruit-api.dragonfruit.network/assets/onion.db" + (useBrotli ? ".br" : string.Empty);

        [RequestParameter(ParameterType.Header, "If-Modified-Since")]
        protected string PreviousDatabaseVersionString => previousDatabaseVersion?.ToString("R");
    }
}