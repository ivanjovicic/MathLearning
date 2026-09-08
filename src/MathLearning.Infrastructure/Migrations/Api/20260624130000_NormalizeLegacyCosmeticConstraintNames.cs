using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260624130000_NormalizeLegacyCosmeticConstraintNames")]
public sealed class NormalizeLegacyCosmeticConstraintNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $migration$
            DECLARE
                constraint_mapping record;
            BEGIN
                FOR constraint_mapping IN
                    SELECT *
                    FROM (VALUES
                        ('user_avatar_configs', 'user_avatar_configs_pkey', 'PK_user_avatar_configs'),
                        ('user_avatar_configs', 'user_avatar_configs_UserId_fkey', 'FK_user_avatar_configs_UserProfiles_UserId'),
                        ('user_avatar_configs', 'user_avatar_configs_AccessoryId_fkey', 'FK_user_avatar_configs_cosmetic_items_AccessoryId'),
                        ('user_avatar_configs', 'user_avatar_configs_BackgroundId_fkey', 'FK_user_avatar_configs_cosmetic_items_BackgroundId'),
                        ('user_avatar_configs', 'user_avatar_configs_ClothingId_fkey', 'FK_user_avatar_configs_cosmetic_items_ClothingId'),
                        ('user_avatar_configs', 'user_avatar_configs_EffectId_fkey', 'FK_user_avatar_configs_cosmetic_items_EffectId'),
                        ('user_avatar_configs', 'user_avatar_configs_EmojiId_fkey', 'FK_user_avatar_configs_cosmetic_items_EmojiId'),
                        ('user_avatar_configs', 'user_avatar_configs_FrameId_fkey', 'FK_user_avatar_configs_cosmetic_items_FrameId'),
                        ('user_avatar_configs', 'user_avatar_configs_HairId_fkey', 'FK_user_avatar_configs_cosmetic_items_HairId'),
                        ('user_avatar_configs', 'user_avatar_configs_LeaderboardDecorationId_fkey', 'FK_user_avatar_configs_cosmetic_items_LeaderboardDecorationId'),
                        ('user_avatar_configs', 'user_avatar_configs_SkinId_fkey', 'FK_user_avatar_configs_cosmetic_items_SkinId'),
                        ('user_cosmetic_inventory', 'user_cosmetic_inventory_pkey', 'PK_user_cosmetic_inventory'),
                        ('user_cosmetic_inventory', 'user_cosmetic_inventory_CosmeticItemId_fkey', 'FK_user_cosmetic_inventory_cosmetic_items_CosmeticItemId')
                    ) AS mappings(table_name, legacy_name, ef_name)
                LOOP
                    IF to_regclass(format('public.%I', constraint_mapping.table_name)) IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM pg_constraint
                           WHERE conrelid = to_regclass(format('public.%I', constraint_mapping.table_name))
                             AND conname = constraint_mapping.legacy_name)
                       AND NOT EXISTS (
                           SELECT 1
                           FROM pg_constraint
                           WHERE conrelid = to_regclass(format('public.%I', constraint_mapping.table_name))
                             AND conname = constraint_mapping.ef_name)
                    THEN
                        EXECUTE format(
                            'ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I',
                            constraint_mapping.table_name,
                            constraint_mapping.legacy_name,
                            constraint_mapping.ef_name);
                    END IF;
                END LOOP;
            END
            $migration$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no-op. This bridge migration only normalizes legacy constraint names
        // so the following historical migration can execute consistently on a fresh database.
    }
}
