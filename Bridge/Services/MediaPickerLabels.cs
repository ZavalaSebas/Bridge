using Bridge.Metadata;
using Bridge.Resources;

namespace Bridge.Services;

internal static class MediaPickerLabels
{
    public static string FieldLabel(string mediaField) => mediaField switch
    {
        "Icon" => Strings.IconLabel,
        "CoverImage" => Strings.CoverImageLabel,
        "BackgroundImage" => Strings.BackgroundImageLabel,
        _ => Strings.Media
    };

    public static string AssetKindLabel(SteamGridDbAssetKind kind) => kind switch
    {
        SteamGridDbAssetKind.Icon => Strings.IconLabel,
        SteamGridDbAssetKind.Cover => Strings.CoverImageLabel,
        SteamGridDbAssetKind.Hero => Strings.BackgroundImageLabel,
        _ => Strings.Media
    };
}
