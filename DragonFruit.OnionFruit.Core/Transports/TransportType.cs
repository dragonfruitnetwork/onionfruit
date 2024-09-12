// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

// ReSharper disable InconsistentNaming

namespace DragonFruit.OnionFruit.Core.Transports
{
    public enum TransportType
    {
        Plain,

        [TransportInfo("meek_lite", "lyrebird", DefaultBridgeKey = "meek-azure")]
        meek,

        [TransportInfo("obfs4", "lyrebird", DefaultBridgeKey = "obfs4")]
        obfs4,

        [TransportInfo("obfs3", "lyrebird")]
        obfs3,

        [TransportInfo("scramblesuit", "lyrebird")]
        scramblesuit,

        [TransportInfo("webtunnel", "lyrebird")]
        webtunnel,

        [TransportInfo("snowflake", DefaultBridgeKey = "snowflake")]
        snowflake,

        [TransportInfo("conjure")]
        conjure
    }
}