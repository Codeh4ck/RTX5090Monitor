namespace Rtx5090Monitoring.Plugins.Msi.Device;

internal static class MsiPsuModels
{
    public const int VendorId = 0x0DB0;
    public const int Ai1300TsProductId = 0xAA6F;
    public const int Ai1600TsProductId = 0x808C;

    public static string? Name(int vendorId, int productId) => vendorId != VendorId ? null : productId switch
    {
        Ai1300TsProductId => "MPG Ai1300TS",
        Ai1600TsProductId => "MPG Ai1600TS",
        _ => null
    };

    public static bool Matches(int productId, string model) => Name(VendorId, productId) is { } name && name == model;
}
