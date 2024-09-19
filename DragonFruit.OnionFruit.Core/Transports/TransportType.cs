// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

// ReSharper disable InconsistentNaming

namespace DragonFruit.OnionFruit.Core.Transports
{
    public enum TransportType
    {
        None = 0,

        [TransportInfo(null)]
        Plain = 1,

        [TransportInfo("meek_lite", "lyrebird", DefaultBridgeKey = "meek-azure")]
        meek = 2,

        [TransportInfo("obfs4", "lyrebird", DefaultBridgeKey = "obfs4")]
        obfs4 = 3,

        [TransportInfo("obfs3", "lyrebird")]
        obfs3 = 4,

        [TransportInfo("scramblesuit", "lyrebird")]
        scramblesuit = 5,

        [TransportInfo("webtunnel", "lyrebird")]
        webtunnel = 6,

        [TransportInfo("snowflake", DefaultBridgeKey = "snowflake")]
        snowflake = 7,

        [TransportInfo("conjure")]
        conjure = 8
    }
}