-- Migration: Add Cosmetic System tables (Avatar Customization - Phase 1)
-- Date: 2026-03-08

-- ─── Cosmetic Seasons ───
CREATE TABLE IF NOT EXISTS cosmetic_seasons (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Description" VARCHAR(1000),
    "ThemeAssetPath" VARCHAR(500),
    "StartDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "EndDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_cosmetic_seasons_active" ON cosmetic_seasons ("IsActive");
CREATE INDEX IF NOT EXISTS "IX_cosmetic_seasons_dates" ON cosmetic_seasons ("StartDate", "EndDate");

-- ─── Cosmetic Items ───
CREATE TABLE IF NOT EXISTS cosmetic_items (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Category" VARCHAR(50) NOT NULL,
    "Rarity" VARCHAR(20) NOT NULL DEFAULT 'common',
    "AssetPath" VARCHAR(500) NOT NULL,
    "PreviewAssetPath" VARCHAR(500),
    "UnlockType" VARCHAR(50) NOT NULL DEFAULT 'default',
    "UnlockCondition" VARCHAR(500),
    "CoinPrice" INTEGER,
    "SeasonId" INTEGER REFERENCES cosmetic_seasons("Id") ON DELETE SET NULL,
    "IsDefault" BOOLEAN NOT NULL DEFAULT FALSE,
    "ReleaseDate" TIMESTAMP WITH TIME ZONE,
    "RetirementDate" TIMESTAMP WITH TIME ZONE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_cosmetic_items_category" ON cosmetic_items ("Category");
CREATE INDEX IF NOT EXISTS "IX_cosmetic_items_rarity" ON cosmetic_items ("Rarity");
CREATE INDEX IF NOT EXISTS "IX_cosmetic_items_category_rarity" ON cosmetic_items ("Category", "Rarity");
CREATE INDEX IF NOT EXISTS "IX_cosmetic_items_season" ON cosmetic_items ("SeasonId");
CREATE INDEX IF NOT EXISTS "IX_cosmetic_items_default" ON cosmetic_items ("IsDefault");

-- ─── User Cosmetic Inventory ───
CREATE TABLE IF NOT EXISTS user_cosmetic_inventory (
    "Id" SERIAL PRIMARY KEY,
    "UserId" VARCHAR(450) NOT NULL,
    "CosmeticItemId" INTEGER NOT NULL REFERENCES cosmetic_items("Id") ON DELETE CASCADE,
    "Source" VARCHAR(50) NOT NULL,
    "SeasonId" INTEGER,
    "UnlockedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_user_cosmetic_inventory_user_item" ON user_cosmetic_inventory ("UserId", "CosmeticItemId");
CREATE INDEX IF NOT EXISTS "IX_user_cosmetic_inventory_user" ON user_cosmetic_inventory ("UserId");
CREATE INDEX IF NOT EXISTS "IX_user_cosmetic_inventory_user_source" ON user_cosmetic_inventory ("UserId", "Source");

-- ─── User Avatar Configuration ───
CREATE TABLE IF NOT EXISTS user_avatar_configs (
    "UserId" VARCHAR(450) PRIMARY KEY REFERENCES "UserProfiles"("UserId") ON DELETE CASCADE,
    "SkinId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "HairId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "ClothingId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "AccessoryId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "EmojiId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "FrameId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "BackgroundId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "EffectId" INTEGER REFERENCES cosmetic_items("Id") ON DELETE SET NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- ─── Seed default cosmetic items (starter items every user gets) ───
INSERT INTO cosmetic_items ("Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "IsDefault", "ReleaseDate")
VALUES
    -- Default skins
    ('Classic Student', 'skin', 'common', 'cosmetics/skin/classic_student.png', 'cosmetics/skin/preview/classic_student.png', 'default', TRUE, NOW()),
    ('Math Explorer', 'skin', 'common', 'cosmetics/skin/math_explorer.png', 'cosmetics/skin/preview/math_explorer.png', 'default', TRUE, NOW()),

    -- Default hair
    ('Short Classic', 'hair', 'common', 'cosmetics/hair/short_classic.png', 'cosmetics/hair/preview/short_classic.png', 'default', TRUE, NOW()),
    ('Long Straight', 'hair', 'common', 'cosmetics/hair/long_straight.png', 'cosmetics/hair/preview/long_straight.png', 'default', TRUE, NOW()),
    ('Curly', 'hair', 'common', 'cosmetics/hair/curly.png', 'cosmetics/hair/preview/curly.png', 'default', TRUE, NOW()),

    -- Default clothing
    ('School Uniform', 'clothing', 'common', 'cosmetics/clothing/school_uniform.png', 'cosmetics/clothing/preview/school_uniform.png', 'default', TRUE, NOW()),
    ('Casual T-Shirt', 'clothing', 'common', 'cosmetics/clothing/casual_tshirt.png', 'cosmetics/clothing/preview/casual_tshirt.png', 'default', TRUE, NOW()),

    -- Default accessories
    ('None', 'accessory', 'common', 'cosmetics/accessory/none.png', 'cosmetics/accessory/preview/none.png', 'default', TRUE, NOW()),

    -- Default frames
    ('Basic Frame', 'frame', 'common', 'cosmetics/frame/basic.png', 'cosmetics/frame/preview/basic.png', 'default', TRUE, NOW()),

    -- Default backgrounds
    ('Classroom', 'background', 'common', 'cosmetics/background/classroom.png', 'cosmetics/background/preview/classroom.png', 'default', TRUE, NOW()),
    ('Math Board', 'background', 'common', 'cosmetics/background/math_board.png', 'cosmetics/background/preview/math_board.png', 'default', TRUE, NOW())
ON CONFLICT DO NOTHING;

-- ─── Seed purchasable cosmetic items (coin shop) ───
INSERT INTO cosmetic_items ("Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "CoinPrice", "IsDefault", "ReleaseDate")
VALUES
    -- Rare skins (coin shop)
    ('Ninja Mathematician', 'skin', 'rare', 'cosmetics/skin/ninja_math.png', 'cosmetics/skin/preview/ninja_math.png', 'shop', 200, FALSE, NOW()),
    ('Robot Student', 'skin', 'rare', 'cosmetics/skin/robot_student.png', 'cosmetics/skin/preview/robot_student.png', 'shop', 200, FALSE, NOW()),

    -- Rare hair
    ('Spiky Blue', 'hair', 'rare', 'cosmetics/hair/spiky_blue.png', 'cosmetics/hair/preview/spiky_blue.png', 'shop', 100, FALSE, NOW()),
    ('Rainbow Ponytail', 'hair', 'rare', 'cosmetics/hair/rainbow_ponytail.png', 'cosmetics/hair/preview/rainbow_ponytail.png', 'shop', 100, FALSE, NOW()),

    -- Rare clothing
    ('Lab Coat', 'clothing', 'rare', 'cosmetics/clothing/lab_coat.png', 'cosmetics/clothing/preview/lab_coat.png', 'shop', 150, FALSE, NOW()),
    ('Hoodie', 'clothing', 'rare', 'cosmetics/clothing/hoodie.png', 'cosmetics/clothing/preview/hoodie.png', 'shop', 150, FALSE, NOW()),

    -- Rare accessories
    ('Calculator Watch', 'accessory', 'rare', 'cosmetics/accessory/calculator_watch.png', 'cosmetics/accessory/preview/calculator_watch.png', 'shop', 100, FALSE, NOW()),
    ('Pi Necklace', 'accessory', 'rare', 'cosmetics/accessory/pi_necklace.png', 'cosmetics/accessory/preview/pi_necklace.png', 'shop', 100, FALSE, NOW()),

    -- Rare frames
    ('Golden Frame', 'frame', 'rare', 'cosmetics/frame/golden.png', 'cosmetics/frame/preview/golden.png', 'shop', 150, FALSE, NOW()),
    ('Neon Frame', 'frame', 'rare', 'cosmetics/frame/neon.png', 'cosmetics/frame/preview/neon.png', 'shop', 150, FALSE, NOW()),

    -- Epic items
    ('Einstein Hair', 'hair', 'epic', 'cosmetics/hair/einstein.png', 'cosmetics/hair/preview/einstein.png', 'shop', 300, FALSE, NOW()),
    ('Professor Outfit', 'clothing', 'epic', 'cosmetics/clothing/professor.png', 'cosmetics/clothing/preview/professor.png', 'shop', 400, FALSE, NOW()),
    ('Starfield Background', 'background', 'epic', 'cosmetics/background/starfield.png', 'cosmetics/background/preview/starfield.png', 'shop', 350, FALSE, NOW()),
    ('Animated Fire Frame', 'frame', 'epic', 'cosmetics/frame/animated_fire.png', 'cosmetics/frame/preview/animated_fire.png', 'shop', 500, FALSE, NOW()),

    -- Legendary items (expensive, special)
    ('Golden Genius Skin', 'skin', 'legendary', 'cosmetics/skin/golden_genius.png', 'cosmetics/skin/preview/golden_genius.png', 'shop', 1000, FALSE, NOW()),
    ('Galaxy Background', 'background', 'legendary', 'cosmetics/background/galaxy.png', 'cosmetics/background/preview/galaxy.png', 'shop', 800, FALSE, NOW())
ON CONFLICT DO NOTHING;

-- ─── Seed achievement-unlocked items (not purchasable) ───
INSERT INTO cosmetic_items ("Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "UnlockCondition", "IsDefault", "ReleaseDate")
VALUES
    -- Streak rewards
    ('7-Day Streak Badge', 'accessory', 'rare', 'cosmetics/accessory/streak_7day.png', 'cosmetics/accessory/preview/streak_7day.png', 'streak', 'streak:7', FALSE, NOW()),
    ('30-Day Streak Crown', 'accessory', 'epic', 'cosmetics/accessory/streak_30day_crown.png', 'cosmetics/accessory/preview/streak_30day_crown.png', 'streak', 'streak:30', FALSE, NOW()),
    ('100-Day Streak Aura', 'effect', 'legendary', 'cosmetics/effect/streak_100day_aura.png', 'cosmetics/effect/preview/streak_100day_aura.png', 'streak', 'streak:100', FALSE, NOW()),

    -- Level rewards
    ('Apprentice Hat', 'accessory', 'common', 'cosmetics/accessory/apprentice_hat.png', 'cosmetics/accessory/preview/apprentice_hat.png', 'level', 'level:5', FALSE, NOW()),
    ('Scholar Robe', 'clothing', 'rare', 'cosmetics/clothing/scholar_robe.png', 'cosmetics/clothing/preview/scholar_robe.png', 'level', 'level:10', FALSE, NOW()),
    ('Master Frame', 'frame', 'epic', 'cosmetics/frame/master.png', 'cosmetics/frame/preview/master.png', 'level', 'level:25', FALSE, NOW()),
    ('Grandmaster Skin', 'skin', 'legendary', 'cosmetics/skin/grandmaster.png', 'cosmetics/skin/preview/grandmaster.png', 'level', 'level:50', FALSE, NOW()),

    -- Leaderboard rewards
    ('Top 10 Trophy Frame', 'frame', 'epic', 'cosmetics/frame/top10_trophy.png', 'cosmetics/frame/preview/top10_trophy.png', 'leaderboard', 'top:10', FALSE, NOW()),
    ('#1 Champion Crown', 'accessory', 'legendary', 'cosmetics/accessory/champion_crown.png', 'cosmetics/accessory/preview/champion_crown.png', 'leaderboard', 'top:1', FALSE, NOW()),

    -- XP milestones
    ('1000 XP Star', 'effect', 'rare', 'cosmetics/effect/xp_1000_star.png', 'cosmetics/effect/preview/xp_1000_star.png', 'xp_milestone', 'xp:1000', FALSE, NOW()),
    ('10000 XP Nova', 'effect', 'epic', 'cosmetics/effect/xp_10000_nova.png', 'cosmetics/effect/preview/xp_10000_nova.png', 'xp_milestone', 'xp:10000', FALSE, NOW())
ON CONFLICT DO NOTHING;
