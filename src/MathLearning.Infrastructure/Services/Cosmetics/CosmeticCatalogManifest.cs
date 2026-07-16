using System.Security.Cryptography;
using System.Text;
using MathLearning.Application.DTOs.Cosmetics;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public static class CosmeticCatalogManifestProvider
{
    private static readonly DateTime StableReleaseDateUtc = new(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

    public static CosmeticCatalogManifest Current { get; } = CreateCurrent();

    private static CosmeticCatalogManifest CreateCurrent()
    {
        const string revisionKey = "catalog-20260716-019";

        var upsertSql = """
            INSERT INTO cosmetic_items ("Key", "Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "IsDefault", "ReleaseDate")
            VALUES
                ('skin_classic_student', 'Classic Student', 'skin', 'common', 'cosmetics/skin/classic_student.png', 'cosmetics/skin/preview/classic_student.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('skin_math_explorer', 'Math Explorer', 'skin', 'common', 'cosmetics/skin/math_explorer.png', 'cosmetics/skin/preview/math_explorer.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_short_classic', 'Short Classic', 'hair', 'common', 'cosmetics/hair/short_classic.png', 'cosmetics/hair/preview/short_classic.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_long_straight', 'Long Straight', 'hair', 'common', 'cosmetics/hair/long_straight.png', 'cosmetics/hair/preview/long_straight.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_curly', 'Curly', 'hair', 'common', 'cosmetics/hair/curly.png', 'cosmetics/hair/preview/curly.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_school_uniform', 'School Uniform', 'clothing', 'common', 'cosmetics/clothing/school_uniform.png', 'cosmetics/clothing/preview/school_uniform.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_casual_tshirt', 'Casual T-Shirt', 'clothing', 'common', 'cosmetics/clothing/casual_tshirt.png', 'cosmetics/clothing/preview/casual_tshirt.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('accessory_none', 'None', 'accessory', 'common', 'cosmetics/accessory/none.png', 'cosmetics/accessory/preview/none.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_basic', 'Basic Frame', 'frame', 'common', 'cosmetics/frame/basic.png', 'cosmetics/frame/preview/basic.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('background_classroom', 'Classroom', 'background', 'common', 'cosmetics/background/classroom.png', 'cosmetics/background/preview/classroom.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('background_math_board', 'Math Board', 'background', 'common', 'cosmetics/background/math_board.png', 'cosmetics/background/preview/math_board.png', 'default', TRUE, TIMESTAMPTZ '2026-07-16 00:00:00+00')
            ON CONFLICT ("Key") DO NOTHING;

            INSERT INTO cosmetic_items ("Key", "Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "CoinPrice", "IsDefault", "ReleaseDate")
            VALUES
                ('skin_ninja_mathematician', 'Ninja Mathematician', 'skin', 'rare', 'cosmetics/skin/ninja_math.png', 'cosmetics/skin/preview/ninja_math.png', 'shop', 200, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('skin_robot_student', 'Robot Student', 'skin', 'rare', 'cosmetics/skin/robot_student.png', 'cosmetics/skin/preview/robot_student.png', 'shop', 200, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_spiky_blue', 'Spiky Blue', 'hair', 'rare', 'cosmetics/hair/spiky_blue.png', 'cosmetics/hair/preview/spiky_blue.png', 'shop', 100, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_rainbow_ponytail', 'Rainbow Ponytail', 'hair', 'rare', 'cosmetics/hair/rainbow_ponytail.png', 'cosmetics/hair/preview/rainbow_ponytail.png', 'shop', 100, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_lab_coat', 'Lab Coat', 'clothing', 'rare', 'cosmetics/clothing/lab_coat.png', 'cosmetics/clothing/preview/lab_coat.png', 'shop', 150, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_hoodie', 'Hoodie', 'clothing', 'rare', 'cosmetics/clothing/hoodie.png', 'cosmetics/clothing/preview/hoodie.png', 'shop', 150, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('accessory_calculator_watch', 'Calculator Watch', 'accessory', 'rare', 'cosmetics/accessory/calculator_watch.png', 'cosmetics/accessory/preview/calculator_watch.png', 'shop', 100, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('accessory_pi_necklace', 'Pi Necklace', 'accessory', 'rare', 'cosmetics/accessory/pi_necklace.png', 'cosmetics/accessory/preview/pi_necklace.png', 'shop', 100, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_golden', 'Golden Frame', 'frame', 'rare', 'cosmetics/frame/golden.png', 'cosmetics/frame/preview/golden.png', 'shop', 150, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_neon', 'Neon Frame', 'frame', 'rare', 'cosmetics/frame/neon.png', 'cosmetics/frame/preview/neon.png', 'shop', 150, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_einstein', 'Einstein Hair', 'hair', 'epic', 'cosmetics/hair/einstein.png', 'cosmetics/hair/preview/einstein.png', 'shop', 300, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_professor_outfit', 'Professor Outfit', 'clothing', 'epic', 'cosmetics/clothing/professor.png', 'cosmetics/clothing/preview/professor.png', 'shop', 400, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('background_starfield', 'Starfield Background', 'background', 'epic', 'cosmetics/background/starfield.png', 'cosmetics/background/preview/starfield.png', 'shop', 350, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_animated_fire', 'Animated Fire Frame', 'frame', 'epic', 'cosmetics/frame/animated_fire.png', 'cosmetics/frame/preview/animated_fire.png', 'shop', 500, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('skin_golden_genius', 'Golden Genius Skin', 'skin', 'legendary', 'cosmetics/skin/golden_genius.png', 'cosmetics/skin/preview/golden_genius.png', 'shop', 1000, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('background_galaxy', 'Galaxy Background', 'background', 'legendary', 'cosmetics/background/galaxy.png', 'cosmetics/background/preview/galaxy.png', 'shop', 800, FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00')
            ON CONFLICT ("Key") DO NOTHING;

            INSERT INTO cosmetic_items ("Key", "Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "UnlockCondition", "IsDefault", "ReleaseDate")
            VALUES
                ('accessory_streak_7day_badge', '7-Day Streak Badge', 'accessory', 'rare', 'cosmetics/accessory/streak_7day.png', 'cosmetics/accessory/preview/streak_7day.png', 'streak', 'streak:7', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('accessory_streak_30day_crown', '30-Day Streak Crown', 'accessory', 'epic', 'cosmetics/accessory/streak_30day_crown.png', 'cosmetics/accessory/preview/streak_30day_crown.png', 'streak', 'streak:30', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('effect_streak_100day_aura', '100-Day Streak Aura', 'effect', 'legendary', 'cosmetics/effect/streak_100day_aura.png', 'cosmetics/effect/preview/streak_100day_aura.png', 'streak', 'streak:100', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('accessory_apprentice_hat', 'Apprentice Hat', 'accessory', 'common', 'cosmetics/accessory/apprentice_hat.png', 'cosmetics/accessory/preview/apprentice_hat.png', 'level', 'level:5', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_scholar_robe', 'Scholar Robe', 'clothing', 'rare', 'cosmetics/clothing/scholar_robe.png', 'cosmetics/clothing/preview/scholar_robe.png', 'level', 'level:10', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_master', 'Master Frame', 'frame', 'epic', 'cosmetics/frame/master.png', 'cosmetics/frame/preview/master.png', 'level', 'level:25', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('skin_grandmaster', 'Grandmaster Skin', 'skin', 'legendary', 'cosmetics/skin/grandmaster.png', 'cosmetics/skin/preview/grandmaster.png', 'level', 'level:50', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_top10_trophy', 'Top 10 Trophy Frame', 'frame', 'epic', 'cosmetics/frame/top10_trophy.png', 'cosmetics/frame/preview/top10_trophy.png', 'leaderboard', 'top:10', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('accessory_champion_crown', '#1 Champion Crown', 'accessory', 'legendary', 'cosmetics/accessory/champion_crown.png', 'cosmetics/accessory/preview/champion_crown.png', 'leaderboard', 'top:1', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('effect_xp_1000_star', '1000 XP Star', 'effect', 'rare', 'cosmetics/effect/xp_1000_star.png', 'cosmetics/effect/preview/xp_1000_star.png', 'xp_milestone', 'xp:1000', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('effect_xp_10000_nova', '10000 XP Nova', 'effect', 'epic', 'cosmetics/effect/xp_10000_nova.png', 'cosmetics/effect/preview/xp_10000_nova.png', 'xp_milestone', 'xp:10000', FALSE, TIMESTAMPTZ '2026-07-16 00:00:00+00')
            ON CONFLICT ("Key") DO NOTHING;

            INSERT INTO cosmetic_items ("Key", "Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "IsDefault", "FragmentLabel", "FragmentsRequired", "ReleaseDate")
            VALUES
                ('skin_default', 'Default Skin', 'skin', 'common', 'cosmetics/skin/default.png', 'cosmetics/skin/preview/default.png', 'default', TRUE, NULL, NULL, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('hair_default', 'Default Hair', 'hair', 'common', 'cosmetics/hair/default.png', 'cosmetics/hair/preview/default.png', 'default', TRUE, NULL, NULL, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('clothing_default', 'Default Clothing', 'clothing', 'common', 'cosmetics/clothing/default.png', 'cosmetics/clothing/preview/default.png', 'default', TRUE, NULL, NULL, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('emoji_default', 'Default Emoji', 'emoji', 'common', 'cosmetics/emoji/default.png', 'cosmetics/emoji/preview/default.png', 'default', TRUE, NULL, NULL, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('bg_default', 'Default Background', 'background', 'common', 'cosmetics/background/default.png', 'cosmetics/background/preview/default.png', 'default', TRUE, NULL, NULL, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('frame_comet', 'Comet Frame', 'frame', 'epic', 'cosmetics/frame/comet.png', 'cosmetics/frame/preview/comet.png', 'fragment', FALSE, 'Comet Frame Fragment', 5, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('effect_nova_trail', 'Nova Trail', 'effect', 'epic', 'cosmetics/effect/nova_trail.png', 'cosmetics/effect/preview/nova_trail.png', 'fragment', FALSE, 'Nova Trail Fragment', 5, TIMESTAMPTZ '2026-07-16 00:00:00+00'),
                ('effect_neon_number_burst', 'Neon Number Burst', 'effect', 'epic', 'cosmetics/effect/neon_number_burst.png', 'cosmetics/effect/preview/neon_number_burst.png', 'fragment', FALSE, 'Neon Number Burst Fragment', 5, TIMESTAMPTZ '2026-07-16 00:00:00+00')
            ON CONFLICT ("Key") DO UPDATE SET
                "FragmentLabel" = EXCLUDED."FragmentLabel",
                "FragmentsRequired" = EXCLUDED."FragmentsRequired",
                "IsDefault" = EXCLUDED."IsDefault",
                "UnlockType" = EXCLUDED."UnlockType";
            """;

        return Create(
            revisionKey,
            upsertSql,
            StableReleaseDateUtc,
            [
                "skin_classic_student",
                "skin_math_explorer",
                "hair_short_classic",
                "hair_long_straight",
                "hair_curly",
                "clothing_school_uniform",
                "clothing_casual_tshirt",
                "accessory_none",
                "frame_basic",
                "background_classroom",
                "background_math_board",
                "skin_default",
                "hair_default",
                "clothing_default",
                "emoji_default",
                "bg_default"
            ],
            [
                new CosmeticCatalogFragmentRequirement("frame_comet", "Comet Frame Fragment", 5),
                new CosmeticCatalogFragmentRequirement("effect_nova_trail", "Nova Trail Fragment", 5),
                new CosmeticCatalogFragmentRequirement("effect_neon_number_burst", "Neon Number Burst Fragment", 5)
            ]);
    }

    public static CosmeticCatalogManifest Create(
        string revisionKey,
        string upsertSql,
        DateTime releaseDateUtc,
        IReadOnlyList<string> requiredDefaultKeys,
        IReadOnlyList<CosmeticCatalogFragmentRequirement> requiredFragments)
    {
        var checksum = ComputeChecksum(revisionKey, upsertSql, releaseDateUtc, requiredDefaultKeys, requiredFragments);
        return new CosmeticCatalogManifest(revisionKey, checksum, upsertSql, releaseDateUtc, requiredDefaultKeys, requiredFragments);
    }

    private static string ComputeChecksum(
        string revisionKey,
        string upsertSql,
        DateTime releaseDateUtc,
        IReadOnlyList<string> requiredDefaultKeys,
        IReadOnlyList<CosmeticCatalogFragmentRequirement> requiredFragments)
    {
        var normalized = string.Join('\n',
            revisionKey.Trim(),
            releaseDateUtc.ToUniversalTime().ToString("O"),
            string.Join('|', requiredDefaultKeys.OrderBy(x => x, StringComparer.Ordinal)),
            string.Join('|', requiredFragments
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}:{x.FragmentLabel}:{x.FragmentsRequired}")),
            upsertSql.Trim());

        var bytes = Encoding.UTF8.GetBytes(normalized);
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
