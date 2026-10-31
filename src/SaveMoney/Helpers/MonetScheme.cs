namespace SaveMoney.Helpers;

/// <summary>Platform-independent, checked-in Material 3 fallback schemes.</summary>
public static class MonetScheme
{
    public const string Device = "device";
    public const string Manual = "manual";
    public const string DefaultKey = "apricot";

    public static IReadOnlyList<(string Key, string Name, string Preview)> Options { get; } =
    [
        ("apricot", "Абрикосовый", "#FFDBCA"),
        ("sage", "Шалфейный", "#BCECCE"),
        ("blue", "Голубой", "#CBE6FF"),
        ("lavender", "Лавандовый", "#E9DDFF"),
        ("rose", "Розовый", "#FFD9E4"),
    ];

    // primary/onPrimary/container/onContainer, secondary equivalents, tertiary equivalents,
    // surface/onSurface/onSurfaceVariant, lowest/low/container/high/highest, outline/outlineVariant.
    private static readonly Dictionary<string, (string Light, string Dark)> Schemes = new()
    {
        ["apricot"] = (
            "8B4F35 FFFFFF FFDBCA 351000 77574A FFFFFF FFDBCA 2C160C 685E30 FFFFFF F0E2A8 211B00 FFF8F5 211A17 53433C FFFFFF FFF1EB FCEBE3 F6E5DD F0DFD8 85736A D8C2B8",
            "FFB694 55200A 6E3821 FFDBCA E7BEAD 442A1F 5D4034 FFDBCA D3C68F 383005 50471A F0E2A8 19120F F0DFD8 D8C2B8 140D0A 211A17 251E1A 302824 3B332E A08D83 53433C"),
        ["sage"] = (
            "32694D FFFFFF BCECCE 002112 506353 FFFFFF D3E8D5 0E1F14 3B6470 FFFFFF BFE9F7 001F27 F5FBF3 171D18 414942 FFFFFF EFF5ED E9EFE7 E3E9E1 DDE4DC 717971 C1C9C0",
            "A1D0B3 003921 185137 BCECCE B7CCB9 233428 394B3D D3E8D5 A4CDDA 073640 234C58 BFE9F7 0F1510 DDE4DC C1C9C0 0A100B 171D18 1B211C 252B26 303630 8B938A 414942"),
        ["blue"] = (
            "28638A FFFFFF CBE6FF 001E30 51606F FFFFFF D5E4F7 0E1D2A 68587A FFFFFF EEDBFF 231533 F7F9FF 191C20 42474E FFFFFF F0F4FC EAEFF6 E4E9F0 DEE3EA 73777F C2C7CF",
            "98CDF8 003350 094B71 CBE6FF B9C8DA 233240 3A4857 D5E4F7 D3BFE6 392A49 504060 EEDBFF 101418 E0E3E9 C2C7CF 0B0F13 191C20 1D2024 272A2F 32353A 8C9198 42474E"),
        ["lavender"] = (
            "67548E FFFFFF E9DDFF 220F46 635B70 FFFFFF E9DEF8 1F182B 7E525E FFFFFF FFD9E2 31101C FDF7FF 1D1B20 49454E FFFFFF F7F1FA F1EBF4 EBE5EE E6DFE9 7A757F CAC4CF",
            "D2BCFD 38265D 4F3D75 E9DDFF CDC2DB 342D40 4B4358 E9DEF8 F1B8C7 4A2531 643B47 FFD9E2 151218 E6DFE9 CAC4CF 100D13 1D1B20 211F24 2C292F 37343A 948F99 49454E"),
        ["rose"] = (
            "8E4964 FFFFFF FFD9E4 3A071F 745660 FFFFFF FFD9E4 2B151E 7C5635 FFFFFF FFDCC0 2D1600 FFF8F8 22191D 514348 FFFFFF FFF0F3 FCE9EE F6E3E8 F0DDE2 847379 D7C1C8",
            "FFB1CC 561D35 72334C FFD9E4 E3BDC9 432A34 5B4049 FFD9E4 EFBD96 48290D 624023 FFDCC0 191115 F0DDE2 D7C1C8 140C0F 22191D 261D21 31272B 3D3236 9E8C92 514348"),
    };

    public static string NormalizeKey(string? key) => key is not null && Schemes.ContainsKey(key) ? key : DefaultKey;

    public static string ResolveSource(string? savedSource, bool hasExistingPreferences, bool supportsDeviceColors) =>
        savedSource is Device or Manual ? savedSource : hasExistingPreferences || !supportsDeviceColors ? Manual : Device;

    public static IReadOnlyDictionary<string, string> Create(string? key, bool dark)
    {
        var scheme = Schemes[NormalizeKey(key)];
        var values = (dark ? scheme.Dark : scheme.Light).Split(' ');
        string[] roles = ["Primary", "OnPrimary", "PrimaryContainer", "OnPrimaryContainer",
            "Secondary", "OnSecondary", "SecondaryContainer", "OnSecondaryContainer",
            "Tertiary", "OnTertiary", "TertiaryContainer", "OnTertiaryContainer",
            "Surface", "OnSurface", "OnSurfaceVariant", "SurfaceContainerLowest", "SurfaceContainerLow",
            "SurfaceContainer", "SurfaceContainerHigh", "SurfaceContainerHighest", "Outline", "OutlineVariant"];
        var result = roles.Select((role, i) => (role, hex: "#" + values[i])).ToDictionary(p => p.role, p => p.hex);
        result["Error"] = dark ? "#FFB4AB" : "#BA1A1A";
        result["OnError"] = dark ? "#690005" : "#FFFFFF";
        result["ErrorContainer"] = dark ? "#93000A" : "#FFDAD6";
        result["OnErrorContainer"] = dark ? "#FFDAD6" : "#410002";
        result["Income"] = dark ? "#9DD5AC" : "#216A3B";
        result["IncomeContainer"] = dark ? "#124D27" : "#B9F2C6";
        result["OnIncomeContainer"] = dark ? "#B9F2C6" : "#00210A";
        result["Warning"] = dark ? "#E7C46D" : "#735B08";
        result["WarningContainer"] = dark ? "#574400" : "#FFE08B";
        result["OnWarningContainer"] = dark ? "#FFE08B" : "#241A00";
        return result;
    }
}
