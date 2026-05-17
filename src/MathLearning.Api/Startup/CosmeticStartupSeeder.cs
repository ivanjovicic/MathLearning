using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MathLearning.Api.Startup;

public static class CosmeticStartupSeeder
{
    public static async Task EnsureCosmeticItemsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        try
        {
            var cosmeticCountBefore = await db.CosmeticItems.CountAsync(ct);
            await db.Database.ExecuteSqlRawAsync(@"
                        INSERT INTO cosmetic_items (""Key"", ""Name"", ""Category"", ""Rarity"", ""AssetPath"", ""PreviewAssetPath"", ""UnlockType"", ""IsDefault"", ""ReleaseDate"")
                        VALUES
                            ('skin_classic_student', 'Classic Student', 'skin', 'common', 'cosmetics/skin/classic_student.png', 'cosmetics/skin/preview/classic_student.png', 'default', TRUE, NOW()),
                            ('skin_math_explorer', 'Math Explorer', 'skin', 'common', 'cosmetics/skin/math_explorer.png', 'cosmetics/skin/preview/math_explorer.png', 'default', TRUE, NOW()),
                            ('hair_short_classic', 'Short Classic', 'hair', 'common', 'cosmetics/hair/short_classic.png', 'cosmetics/hair/preview/short_classic.png', 'default', TRUE, NOW()),
                            ('hair_long_straight', 'Long Straight', 'hair', 'common', 'cosmetics/hair/long_straight.png', 'cosmetics/hair/preview/long_straight.png', 'default', TRUE, NOW()),
                            ('hair_curly', 'Curly', 'hair', 'common', 'cosmetics/hair/curly.png', 'cosmetics/hair/preview/curly.png', 'default', TRUE, NOW()),
                            ('clothing_school_uniform', 'School Uniform', 'clothing', 'common', 'cosmetics/clothing/school_uniform.png', 'cosmetics/clothing/preview/school_uniform.png', 'default', TRUE, NOW()),
                            ('clothing_casual_tshirt', 'Casual T-Shirt', 'clothing', 'common', 'cosmetics/clothing/casual_tshirt.png', 'cosmetics/clothing/preview/casual_tshirt.png', 'default', TRUE, NOW()),
                            ('accessory_none', 'None', 'accessory', 'common', 'cosmetics/accessory/none.png', 'cosmetics/accessory/preview/none.png', 'default', TRUE, NOW()),
                            ('frame_basic', 'Basic Frame', 'frame', 'common', 'cosmetics/frame/basic.png', 'cosmetics/frame/preview/basic.png', 'default', TRUE, NOW()),
                            ('background_classroom', 'Classroom', 'background', 'common', 'cosmetics/background/classroom.png', 'cosmetics/background/preview/classroom.png', 'default', TRUE, NOW()),
                            ('background_math_board', 'Math Board', 'background', 'common', 'cosmetics/background/math_board.png', 'cosmetics/background/preview/math_board.png', 'default', TRUE, NOW())
                        ON CONFLICT (""Key"") DO NOTHING;

                        INSERT INTO cosmetic_items (""Key"", ""Name"", ""Category"", ""Rarity"", ""AssetPath"", ""PreviewAssetPath"", ""UnlockType"", ""CoinPrice"", ""IsDefault"", ""ReleaseDate"")
                        VALUES
                            ('skin_ninja_mathematician', 'Ninja Mathematician', 'skin', 'rare', 'cosmetics/skin/ninja_math.png', 'cosmetics/skin/preview/ninja_math.png', 'shop', 200, FALSE, NOW()),
                            ('skin_robot_student', 'Robot Student', 'skin', 'rare', 'cosmetics/skin/robot_student.png', 'cosmetics/skin/preview/robot_student.png', 'shop', 200, FALSE, NOW()),
                            ('hair_spiky_blue', 'Spiky Blue', 'hair', 'rare', 'cosmetics/hair/spiky_blue.png', 'cosmetics/hair/preview/spiky_blue.png', 'shop', 100, FALSE, NOW()),
                            ('hair_rainbow_ponytail', 'Rainbow Ponytail', 'hair', 'rare', 'cosmetics/hair/rainbow_ponytail.png', 'cosmetics/hair/preview/rainbow_ponytail.png', 'shop', 100, FALSE, NOW()),
                            ('clothing_lab_coat', 'Lab Coat', 'clothing', 'rare', 'cosmetics/clothing/lab_coat.png', 'cosmetics/clothing/preview/lab_coat.png', 'shop', 150, FALSE, NOW()),
                            ('clothing_hoodie', 'Hoodie', 'clothing', 'rare', 'cosmetics/clothing/hoodie.png', 'cosmetics/clothing/preview/hoodie.png', 'shop', 150, FALSE, NOW()),
                            ('accessory_calculator_watch', 'Calculator Watch', 'accessory', 'rare', 'cosmetics/accessory/calculator_watch.png', 'cosmetics/accessory/preview/calculator_watch.png', 'shop', 100, FALSE, NOW()),
                            ('accessory_pi_necklace', 'Pi Necklace', 'accessory', 'rare', 'cosmetics/accessory/pi_necklace.png', 'cosmetics/accessory/preview/pi_necklace.png', 'shop', 100, FALSE, NOW()),
                            ('frame_golden', 'Golden Frame', 'frame', 'rare', 'cosmetics/frame/golden.png', 'cosmetics/frame/preview/golden.png', 'shop', 150, FALSE, NOW()),
                            ('frame_neon', 'Neon Frame', 'frame', 'rare', 'cosmetics/frame/neon.png', 'cosmetics/frame/preview/neon.png', 'shop', 150, FALSE, NOW()),
                            ('hair_einstein', 'Einstein Hair', 'hair', 'epic', 'cosmetics/hair/einstein.png', 'cosmetics/hair/preview/einstein.png', 'shop', 300, FALSE, NOW()),
                            ('clothing_professor_outfit', 'Professor Outfit', 'clothing', 'epic', 'cosmetics/clothing/professor.png', 'cosmetics/clothing/preview/professor.png', 'shop', 400, FALSE, NOW()),
                            ('background_starfield', 'Starfield Background', 'background', 'epic', 'cosmetics/background/starfield.png', 'cosmetics/background/preview/starfield.png', 'shop', 350, FALSE, NOW()),
                            ('frame_animated_fire', 'Animated Fire Frame', 'frame', 'epic', 'cosmetics/frame/animated_fire.png', 'cosmetics/frame/preview/animated_fire.png', 'shop', 500, FALSE, NOW()),
                            ('skin_golden_genius', 'Golden Genius Skin', 'skin', 'legendary', 'cosmetics/skin/golden_genius.png', 'cosmetics/skin/preview/golden_genius.png', 'shop', 1000, FALSE, NOW()),
                            ('background_galaxy', 'Galaxy Background', 'background', 'legendary', 'cosmetics/background/galaxy.png', 'cosmetics/background/preview/galaxy.png', 'shop', 800, FALSE, NOW())
                        ON CONFLICT (""Key"") DO NOTHING;

                        INSERT INTO cosmetic_items (""Key"", ""Name"", ""Category"", ""Rarity"", ""AssetPath"", ""PreviewAssetPath"", ""UnlockType"", ""UnlockCondition"", ""IsDefault"", ""ReleaseDate"")
                        VALUES
                            ('accessory_streak_7day_badge', '7-Day Streak Badge', 'accessory', 'rare', 'cosmetics/accessory/streak_7day.png', 'cosmetics/accessory/preview/streak_7day.png', 'streak', 'streak:7', FALSE, NOW()),
                            ('accessory_streak_30day_crown', '30-Day Streak Crown', 'accessory', 'epic', 'cosmetics/accessory/streak_30day_crown.png', 'cosmetics/accessory/preview/streak_30day_crown.png', 'streak', 'streak:30', FALSE, NOW()),
                            ('effect_streak_100day_aura', '100-Day Streak Aura', 'effect', 'legendary', 'cosmetics/effect/streak_100day_aura.png', 'cosmetics/effect/preview/streak_100day_aura.png', 'streak', 'streak:100', FALSE, NOW()),
                            ('accessory_apprentice_hat', 'Apprentice Hat', 'accessory', 'common', 'cosmetics/accessory/apprentice_hat.png', 'cosmetics/accessory/preview/apprentice_hat.png', 'level', 'level:5', FALSE, NOW()),
                            ('clothing_scholar_robe', 'Scholar Robe', 'clothing', 'rare', 'cosmetics/clothing/scholar_robe.png', 'cosmetics/clothing/preview/scholar_robe.png', 'level', 'level:10', FALSE, NOW()),
                            ('frame_master', 'Master Frame', 'frame', 'epic', 'cosmetics/frame/master.png', 'cosmetics/frame/preview/master.png', 'level', 'level:25', FALSE, NOW()),
                            ('skin_grandmaster', 'Grandmaster Skin', 'skin', 'legendary', 'cosmetics/skin/grandmaster.png', 'cosmetics/skin/preview/grandmaster.png', 'level', 'level:50', FALSE, NOW()),
                            ('frame_top10_trophy', 'Top 10 Trophy Frame', 'frame', 'epic', 'cosmetics/frame/top10_trophy.png', 'cosmetics/frame/preview/top10_trophy.png', 'leaderboard', 'top:10', FALSE, NOW()),
                            ('accessory_champion_crown', '#1 Champion Crown', 'accessory', 'legendary', 'cosmetics/accessory/champion_crown.png', 'cosmetics/accessory/preview/champion_crown.png', 'leaderboard', 'top:1', FALSE, NOW()),
                            ('effect_xp_1000_star', '1000 XP Star', 'effect', 'rare', 'cosmetics/effect/xp_1000_star.png', 'cosmetics/effect/preview/xp_1000_star.png', 'xp_milestone', 'xp:1000', FALSE, NOW()),
                            ('effect_xp_10000_nova', '10000 XP Nova', 'effect', 'epic', 'cosmetics/effect/xp_10000_nova.png', 'cosmetics/effect/preview/xp_10000_nova.png', 'xp_milestone', 'xp:10000', FALSE, NOW())
                        ON CONFLICT (""Key"") DO NOTHING;
                    ");

            var cosmeticCountAfter = await db.CosmeticItems.CountAsync(ct);
            if (cosmeticCountAfter > cosmeticCountBefore)
            {
                Log.Information("Cosmetic items ensured on startup. Count={Count}", cosmeticCountAfter);
            }
            else
            {
                Log.Information("Cosmetic system tables verified ({Count} items exist)", cosmeticCountAfter);
            }
        }
        catch (Exception cosmeticEx)
        {
            Log.Warning(cosmeticEx, "Could not seed cosmetic items");
        }
    }
}
