// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

// ReSharper disable InconsistentNaming

namespace DragonFruit.OnionFruit.Core.Transports
{
    public enum TransportType
    {
        Plain = 0,

        [TransportInfo("meek_lite", "lyrebird", DefaultBridgeKey = "meek-azure")]
        meek = 1,

        [TransportInfo("obfs4", "lyrebird", DefaultBridgeKey = "obfs4")]
        obfs4 = 2,

        [TransportInfo("obfs3", "lyrebird")]
        obfs3 = 3,

        [TransportInfo("scramblesuit", "lyrebird")]
        scramblesuit = 4,

        [TransportInfo("webtunnel", "lyrebird")]
        webtunnel = 5,

        [TransportInfo("snowflake", DefaultBridgeKey = "snowflake")]
        snowflake = 6,

        [TransportInfo("conjure")]
        conjure = 7
    }
}