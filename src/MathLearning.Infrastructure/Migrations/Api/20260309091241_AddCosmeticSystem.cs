using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddCosmeticSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: all tables may already exist from raw-SQL startup code.
            // Use DO $$ blocks with IF NOT EXISTS for table creation and ALTER TABLE for columns.

            migrationBuilder.Sql(@"
                -- 1. cosmetic_seasons
                CREATE TABLE IF NOT EXISTS cosmetic_seasons (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Key"" VARCHAR(64) NOT NULL DEFAULT '',
                    ""Name"" VARCHAR(200) NOT NULL,
                    ""Description"" VARCHAR(1000),
                    ""Theme"" VARCHAR(128),
                    ""ThemeAssetPath"" VARCHAR(500),
                    ""Status"" VARCHAR(32) NOT NULL DEFAULT 'draft',
                    ""StartDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                    ""EndDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                    ""RewardLockAt"" TIMESTAMP WITH TIME ZONE,
                    ""ArchiveAt"" TIMESTAMP WITH TIME ZONE,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                -- Ensure all columns exist (backfill from raw-SQL era)
                ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""Key"" VARCHAR(64) NOT NULL DEFAULT '';
                ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""Theme"" VARCHAR(128);
                ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""Status"" VARCHAR(32) NOT NULL DEFAULT 'draft';
                ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""RewardLockAt"" TIMESTAMP WITH TIME ZONE;
                ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""ArchiveAt"" TIMESTAMP WITH TIME ZONE;
                ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_seasons_key"" ON cosmetic_seasons (""Key"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_seasons_active"" ON cosmetic_seasons (""IsActive"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_seasons_dates"" ON cosmetic_seasons (""StartDate"", ""EndDate"");

                -- 2. cosmetic_items
                CREATE TABLE IF NOT EXISTS cosmetic_items (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Key"" VARCHAR(64) NOT NULL DEFAULT '',
                    ""Name"" VARCHAR(200) NOT NULL,
                    ""Category"" VARCHAR(50) NOT NULL,
                    ""Rarity"" VARCHAR(20) NOT NULL DEFAULT 'common',
                    ""AssetPath"" VARCHAR(500) NOT NULL,
                    ""PreviewAssetPath"" VARCHAR(500),
                    ""UnlockType"" VARCHAR(50) NOT NULL DEFAULT 'default',
                    ""UnlockCondition"" VARCHAR(500),
                    ""UnlockConditionJson"" jsonb,
                    ""CompatibilityRulesJson"" jsonb,
                    ""CoinPrice"" INTEGER,
                    ""SeasonId"" INTEGER REFERENCES cosmetic_seasons(""Id"") ON DELETE SET NULL,
                    ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""IsHidden"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                    ""AssetVersion"" VARCHAR(32) NOT NULL DEFAULT '1',
                    ""ReleaseDate"" TIMESTAMP WITH TIME ZONE,
                    ""RetirementDate"" TIMESTAMP WITH TIME ZONE,
                    ""MetadataJson"" jsonb,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""Key"" VARCHAR(64) NOT NULL DEFAULT '';
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""UnlockConditionJson"" jsonb;
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""CompatibilityRulesJson"" jsonb;
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""IsHidden"" BOOLEAN NOT NULL DEFAULT FALSE;
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""SortOrder"" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""AssetVersion"" VARCHAR(32) NOT NULL DEFAULT '1';
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""MetadataJson"" jsonb;
                ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_items_key"" ON cosmetic_items (""Key"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_active_release"" ON cosmetic_items (""IsActive"", ""ReleaseDate"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_category"" ON cosmetic_items (""Category"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_category_rarity"" ON cosmetic_items (""Category"", ""Rarity"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_default"" ON cosmetic_items (""IsDefault"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_rarity"" ON cosmetic_items (""Rarity"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_season"" ON cosmetic_items (""SeasonId"");

                -- 3. user_cosmetic_inventory
                CREATE TABLE IF NOT EXISTS user_cosmetic_inventory (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" VARCHAR(450) NOT NULL,
                    ""CosmeticItemId"" INTEGER NOT NULL REFERENCES cosmetic_items(""Id"") ON DELETE CASCADE,
                    ""Source"" VARCHAR(50) NOT NULL,
                    ""SourceRef"" VARCHAR(128),
                    ""GrantReason"" VARCHAR(256),
                    ""SeasonId"" INTEGER,
                    ""AssetVersion"" VARCHAR(32) NOT NULL DEFAULT '1',
                    ""IsRevoked"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""RevokedAt"" TIMESTAMP WITH TIME ZONE,
                    ""UnlockedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""SourceRef"" VARCHAR(128);
                ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""GrantReason"" VARCHAR(256);
                ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""AssetVersion"" VARCHAR(32) NOT NULL DEFAULT '1';
                ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""IsRevoked"" BOOLEAN NOT NULL DEFAULT FALSE;
                ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""RevokedAt"" TIMESTAMP WITH TIME ZONE;

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_user_cosmetic_inventory_user_item"" ON user_cosmetic_inventory (""UserId"", ""CosmeticItemId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_user"" ON user_cosmetic_inventory (""UserId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_user_source"" ON user_cosmetic_inventory (""UserId"", ""Source"");
                CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_source_ref"" ON user_cosmetic_inventory (""Source"", ""SourceRef"");
                CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_CosmeticItemId"" ON user_cosmetic_inventory (""CosmeticItemId"");

                -- 4. user_avatar_configs
                CREATE TABLE IF NOT EXISTS user_avatar_configs (
                    ""UserId"" VARCHAR(450) PRIMARY KEY REFERENCES ""UserProfiles""(""UserId"") ON DELETE CASCADE,
                    ""SkinId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""HairId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""ClothingId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""AccessoryId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""EmojiId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""FrameId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""BackgroundId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""EffectId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""LeaderboardDecorationId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""Version"" BIGINT NOT NULL DEFAULT 0,
                    ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                ALTER TABLE user_avatar_configs ADD COLUMN IF NOT EXISTS ""LeaderboardDecorationId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL;
                ALTER TABLE user_avatar_configs ADD COLUMN IF NOT EXISTS ""Version"" BIGINT NOT NULL DEFAULT 0;

                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_AccessoryId"" ON user_avatar_configs (""AccessoryId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_BackgroundId"" ON user_avatar_configs (""BackgroundId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_ClothingId"" ON user_avatar_configs (""ClothingId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_EffectId"" ON user_avatar_configs (""EffectId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_EmojiId"" ON user_avatar_configs (""EmojiId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_FrameId"" ON user_avatar_configs (""FrameId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_HairId"" ON user_avatar_configs (""HairId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_LeaderboardDecorationId"" ON user_avatar_configs (""LeaderboardDecorationId"");
                CREATE INDEX IF NOT EXISTS ""IX_user_avatar_configs_SkinId"" ON user_avatar_configs (""SkinId"");

                -- 5. cosmetic_reward_rules
                CREATE TABLE IF NOT EXISTS cosmetic_reward_rules (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Key"" VARCHAR(128) NOT NULL,
                    ""SourceType"" VARCHAR(64) NOT NULL,
                    ""ConditionJson"" jsonb,
                    ""RewardType"" VARCHAR(64) NOT NULL,
                    ""RewardPayloadJson"" jsonb NOT NULL,
                    ""Priority"" INTEGER NOT NULL DEFAULT 0,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""UpdatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_reward_rules_key"" ON cosmetic_reward_rules (""Key"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_reward_rules_source_active_priority"" ON cosmetic_reward_rules (""SourceType"", ""IsActive"", ""Priority"");

                -- 6. cosmetic_reward_claims
                CREATE TABLE IF NOT EXISTS cosmetic_reward_claims (
                    ""Id"" UUID PRIMARY KEY,
                    ""UserId"" VARCHAR(450) NOT NULL,
                    ""RewardKey"" VARCHAR(128) NOT NULL,
                    ""SourceType"" VARCHAR(64) NOT NULL,
                    ""SourceRef"" VARCHAR(128) NOT NULL,
                    ""CosmeticItemId"" INTEGER NOT NULL REFERENCES cosmetic_items(""Id"") ON DELETE CASCADE,
                    ""ClaimedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_reward_claims_user_reward_source"" ON cosmetic_reward_claims (""UserId"", ""RewardKey"", ""SourceRef"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_reward_claims_user_claimed_at"" ON cosmetic_reward_claims (""UserId"", ""ClaimedAtUtc"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_reward_claims_CosmeticItemId"" ON cosmetic_reward_claims (""CosmeticItemId"");

                -- 7. season_reward_tracks
                CREATE TABLE IF NOT EXISTS season_reward_tracks (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""SeasonId"" INTEGER NOT NULL REFERENCES cosmetic_seasons(""Id"") ON DELETE CASCADE,
                    ""TrackType"" VARCHAR(32) NOT NULL,
                    ""Tier"" INTEGER NOT NULL,
                    ""XpRequired"" INTEGER NOT NULL,
                    ""RewardType"" VARCHAR(64) NOT NULL,
                    ""RewardPayloadJson"" jsonb NOT NULL,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_season_reward_tracks_season_track_tier"" ON season_reward_tracks (""SeasonId"", ""TrackType"", ""Tier"");

                -- 8. user_appearance_projection
                CREATE TABLE IF NOT EXISTS user_appearance_projection (
                    ""UserId"" VARCHAR(450) PRIMARY KEY,
                    ""AvatarVersion"" BIGINT NOT NULL DEFAULT 0,
                    ""SkinId"" INTEGER,
                    ""HairId"" INTEGER,
                    ""ClothingId"" INTEGER,
                    ""AccessoryId"" INTEGER,
                    ""EmojiId"" INTEGER,
                    ""FrameId"" INTEGER,
                    ""BackgroundId"" INTEGER,
                    ""EffectId"" INTEGER,
                    ""LeaderboardDecorationId"" INTEGER,
                    ""SkinAssetPath"" TEXT,
                    ""HairAssetPath"" TEXT,
                    ""ClothingAssetPath"" TEXT,
                    ""AccessoryAssetPath"" TEXT,
                    ""EmojiAssetPath"" TEXT,
                    ""FrameAssetPath"" TEXT,
                    ""BackgroundAssetPath"" TEXT,
                    ""EffectAssetPath"" TEXT,
                    ""LeaderboardDecorationAssetPath"" TEXT,
                    ""UpdatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                -- 9. cosmetic_telemetry_events
                CREATE TABLE IF NOT EXISTS cosmetic_telemetry_events (
                    ""Id"" UUID PRIMARY KEY,
                    ""EventType"" VARCHAR(64) NOT NULL,
                    ""UserId"" VARCHAR(450) NOT NULL,
                    ""CosmeticItemId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                    ""SeasonId"" INTEGER REFERENCES cosmetic_seasons(""Id"") ON DELETE SET NULL,
                    ""MetadataJson"" jsonb,
                    ""OccurredAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_telemetry_events_type_occurred"" ON cosmetic_telemetry_events (""EventType"", ""OccurredAtUtc"");
                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_telemetry_events_user_occurred"" ON cosmetic_telemetry_events (""UserId"", ""OccurredAtUtc"");

                -- 10. cosmetic_audit_log
                CREATE TABLE IF NOT EXISTS cosmetic_audit_log (
                    ""Id"" UUID PRIMARY KEY,
                    ""Action"" VARCHAR(64) NOT NULL,
                    ""ActorUserId"" VARCHAR(450),
                    ""EntityType"" VARCHAR(64) NOT NULL,
                    ""EntityId"" VARCHAR(128) NOT NULL,
                    ""BeforeJson"" jsonb,
                    ""AfterJson"" jsonb,
                    ""OccurredAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ""IX_cosmetic_audit_log_entity_occurred"" ON cosmetic_audit_log (""EntityType"", ""EntityId"", ""OccurredAtUtc"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cosmetic_audit_log");

            migrationBuilder.DropTable(
                name: "cosmetic_reward_claims");

            migrationBuilder.DropTable(
                name: "cosmetic_reward_rules");

            migrationBuilder.DropTable(
                name: "cosmetic_telemetry_events");

            migrationBuilder.DropTable(
                name: "season_reward_tracks");

            migrationBuilder.DropTable(
                name: "user_appearance_projection");

            migrationBuilder.DropTable(
                name: "user_avatar_configs");

            migrationBuilder.DropTable(
                name: "user_cosmetic_inventory");

            migrationBuilder.DropTable(
                name: "cosmetic_items");

            migrationBuilder.DropTable(
                name: "cosmetic_seasons");
        }
    }
}
