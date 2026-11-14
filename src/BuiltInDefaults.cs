namespace IDCXray;

/// <summary>First-run Oracle profile supplied by the user.</summary>
internal static class BuiltInDefaults
{
    public static Node CreateOracleNode() => new()
    {
        Name = "Oracle",
        Protocol = "vless",
        Host = "129.225.142.235",
        Port = 38501,
        Id = "05c3dd29-5884-4764-affd-5f491c4f0cc8",
        Flow = "xtls-rprx-vision",
        Network = "raw",
        Security = "reality",
        Sni = "www.microsoft.com",
        Fingerprint = "chrome",
        PublicKey = "ah3HXRcPMIa-lAGejeVbUxF2-ldwnQiMOYyUqKc_NRQ",
        ShortId = "127acd237767c2da",
        IsBuiltInOracle = true
    };
}
