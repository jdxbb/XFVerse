using System.Windows.Media;

namespace MediaLibrary.App.Helpers;

public static class PersonaPosterPaletteResource
{
    private const string DefaultPersonaKey = "genre_explorer";
    private const string DefaultGender = "female";
    private static readonly PosterBackdropPalette DefaultPalette = Palette(246, 105, 40, 40, 141, 113, 128, 65, 200);
    private static readonly IReadOnlyDictionary<string, PosterBackdropPalette> Palettes =
        new Dictionary<string, PosterBackdropPalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["emotion_immersive:female"] = Palette(178, 73, 40, 61, 46, 164, 40, 49, 181),
            ["emotion_immersive:male"] = Palette(186, 71, 40, 65, 48, 170, 40, 53, 203),
            ["mystery_solver:female"] = Palette(191, 88, 40, 143, 105, 87, 168, 102, 96),
            ["mystery_solver:male"] = Palette(187, 90, 40, 141, 105, 88, 165, 104, 96),
            ["genre_explorer:female"] = Palette(246, 105, 40, 40, 141, 113, 128, 65, 200),
            ["genre_explorer:male"] = Palette(246, 107, 40, 40, 147, 120, 40, 73, 85),
            ["classic_collector:female"] = Palette(143, 70, 40, 120, 96, 84, 150, 95, 95),
            ["classic_collector:male"] = Palette(143, 70, 40, 120, 96, 86, 150, 95, 97),
            ["healing_companion:female"] = Palette(246, 91, 40, 184, 105, 65, 199, 103, 77),
            ["healing_companion:male"] = Palette(229, 87, 40, 170, 105, 67, 189, 102, 78),
            ["rating_curator:female"] = Palette(160, 81, 40, 127, 102, 88, 157, 100, 96),
            ["rating_curator:male"] = Palette(156, 85, 40, 124, 104, 91, 154, 102, 98),
            ["auteur_follower:female"] = Palette(135, 60, 40, 118, 92, 86, 147, 94, 96),
            ["auteur_follower:male"] = Palette(137, 64, 40, 117, 95, 85, 149, 94, 94),
            ["sci_fantasy_traveler:female"] = Palette(44, 62, 131, 163, 86, 58, 218, 108, 40),
            ["sci_fantasy_traveler:male"] = Palette(45, 61, 133, 210, 107, 40, 157, 84, 58),
            ["realism_observer:female"] = Palette(150, 84, 40, 42, 66, 86, 40, 65, 75),
            ["realism_observer:male"] = Palette(168, 97, 40, 47, 67, 86, 65, 70, 114),
            ["action_player:female"] = Palette(183, 73, 40, 145, 98, 78, 167, 96, 90),
            ["action_player:male"] = Palette(186, 75, 40, 146, 99, 77, 170, 97, 87),
            ["arthouse_aesthete:female"] = Palette(159, 100, 50, 122, 111, 105, 153, 107, 109),
            ["arthouse_aesthete:male"] = Palette(146, 97, 55, 113, 109, 109, 146, 107, 115),
            ["thriller_atmosphere_fan:female"] = Palette(40, 80, 207, 107, 63, 51, 120, 88, 129),
            ["thriller_atmosphere_fan:male"] = Palette(44, 78, 189, 102, 61, 57, 100, 62, 66),
            ["dark_humorist:female"] = Palette(199, 110, 49, 110, 51, 100, 126, 62, 96),
            ["dark_humorist:male"] = Palette(183, 104, 50, 96, 48, 105, 119, 53, 109),
            ["romantic_dreamer:female"] = Palette(150, 79, 40, 103, 65, 81, 145, 88, 98),
            ["romantic_dreamer:male"] = Palette(156, 91, 42, 118, 76, 96, 116, 76, 106),
            ["dark_curiosity_seeker:female"] = Palette(171, 90, 40, 78, 58, 99, 64, 58, 100),
            ["dark_curiosity_seeker:male"] = Palette(163, 88, 40, 77, 55, 97, 110, 74, 92),
            ["epic_worldbuilder:female"] = Palette(190, 93, 40, 143, 109, 78, 168, 105, 89),
            ["epic_worldbuilder:male"] = Palette(196, 93, 40, 147, 109, 79, 172, 105, 89),
            ["easy_entertainment_fan:female"] = Palette(246, 132, 40, 136, 168, 107, 97, 145, 141),
            ["easy_entertainment_fan:male"] = Palette(246, 131, 40, 73, 126, 136, 99, 142, 132),
            ["human_nature_analyst:female"] = Palette(123, 62, 46, 102, 57, 94, 139, 81, 109),
            ["human_nature_analyst:male"] = Palette(132, 57, 43, 98, 51, 89, 143, 77, 105),
            ["nostalgia_time_traveler:female"] = Palette(136, 65, 40, 115, 95, 85, 147, 94, 96),
            ["nostalgia_time_traveler:male"] = Palette(139, 68, 40, 118, 96, 88, 147, 95, 97),
            ["niche_treasure_hunter:female"] = Palette(155, 72, 40, 126, 98, 80, 155, 97, 91),
            ["niche_treasure_hunter:male"] = Palette(158, 77, 40, 126, 101, 79, 155, 100, 90),
            ["comedy_relief_fan:female"] = Palette(159, 62, 40, 129, 91, 85, 157, 93, 93),
            ["comedy_relief_fan:male"] = Palette(158, 65, 40, 127, 94, 84, 156, 95, 93),
            ["animation_narrative_fan:female"] = Palette(153, 82, 40, 118, 102, 92, 151, 100, 100),
            ["animation_narrative_fan:male"] = Palette(160, 85, 40, 40, 49, 63, 123, 85, 91),
            ["documentary_truth_seeker:female"] = Palette(134, 83, 45, 111, 103, 103, 143, 102, 108),
            ["documentary_truth_seeker:male"] = Palette(136, 86, 49, 111, 105, 105, 143, 103, 111),
        };

    public static PosterBackdropPalette GetPalette(string personaKey, string gender)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(personaKey)
            ? DefaultPersonaKey
            : personaKey.Trim();
        var normalizedGender = string.Equals(gender, "male", StringComparison.OrdinalIgnoreCase)
            ? "male"
            : DefaultGender;

        return TryGetPalette(normalizedKey, normalizedGender, out var exact)
            ? exact
            : TryGetPalette(normalizedKey, DefaultGender, out var femaleFallback)
                ? femaleFallback
                : TryGetPalette(DefaultPersonaKey, DefaultGender, out var defaultFallback)
                    ? defaultFallback
                    : DefaultPalette;
    }

    private static bool TryGetPalette(string personaKey, string gender, out PosterBackdropPalette palette)
    {
        return Palettes.TryGetValue($"{personaKey}:{gender}", out palette);
    }

    private static PosterBackdropPalette Palette(
        byte primaryRed,
        byte primaryGreen,
        byte primaryBlue,
        byte secondaryRed,
        byte secondaryGreen,
        byte secondaryBlue,
        byte accentRed,
        byte accentGreen,
        byte accentBlue)
    {
        return new PosterBackdropPalette(
            Color.FromRgb(primaryRed, primaryGreen, primaryBlue),
            Color.FromRgb(secondaryRed, secondaryGreen, secondaryBlue),
            Color.FromRgb(accentRed, accentGreen, accentBlue));
    }
}
