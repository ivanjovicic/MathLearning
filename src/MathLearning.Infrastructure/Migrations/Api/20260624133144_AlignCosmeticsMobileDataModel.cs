using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

public partial class AlignCosmeticsMobileDataModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FragmentLabel",
            table: "cosmetic_items",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FragmentsRequired",
            table: "cosmetic_items",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            INSERT INTO cosmetic_items ("Key", "Name", "Category", "Rarity", "AssetPath", "PreviewAssetPath", "UnlockType", "IsDefault", "FragmentLabel", "FragmentsRequired", "ReleaseDate")
            VALUES
                ('skin_default', 'Default Skin', 'skin', 'common', 'cosmetics/skin/default.png', 'cosmetics/skin/preview/default.png', 'default', TRUE, NULL, NULL, NOW()),
                ('hair_default', 'Default Hair', 'hair', 'common', 'cosmetics/hair/default.png', 'cosmetics/hair/preview/default.png', 'default', TRUE, NULL, NULL, NOW()),
                ('clothing_default', 'Default Clothing', 'clothing', 'common', 'cosmetics/clothing/default.png', 'cosmetics/clothing/preview/default.png', 'default', TRUE, NULL, NULL, NOW()),
                ('emoji_default', 'Default Emoji', 'emoji', 'common', 'cosmetics/emoji/default.png', 'cosmetics/emoji/preview/default.png', 'default', TRUE, NULL, NULL, NOW()),
                ('bg_default', 'Default Background', 'background', 'common', 'cosmetics/background/default.png', 'cosmetics/background/preview/default.png', 'default', TRUE, NULL, NULL, NOW()),
                ('frame_comet', 'Comet Frame', 'frame', 'epic', 'cosmetics/frame/comet.png', 'cosmetics/frame/preview/comet.png', 'fragment', FALSE, 'Comet Frame Fragment', 5, NOW()),
                ('effect_nova_trail', 'Nova Trail', 'effect', 'epic', 'cosmetics/effect/nova_trail.png', 'cosmetics/effect/preview/nova_trail.png', 'fragment', FALSE, 'Nova Trail Fragment', 5, NOW()),
                ('effect_neon_number_burst', 'Neon Number Burst', 'effect', 'epic', 'cosmetics/effect/neon_number_burst.png', 'cosmetics/effect/preview/neon_number_burst.png', 'fragment', FALSE, 'Neon Number Burst Fragment', 5, NOW())
            ON CONFLICT ("Key") DO UPDATE SET
                "FragmentLabel" = EXCLUDED."FragmentLabel",
                "FragmentsRequired" = EXCLUDED."FragmentsRequired",
                "IsDefault" = EXCLUDED."IsDefault",
                "UnlockType" = EXCLUDED."UnlockType";
            """);

        migrationBuilder.AddColumn<int>(
            name: "Collected",
            table: "user_cosmetic_fragment_progress",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "CosmeticItemId",
            table: "user_cosmetic_fragment_progress",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Required",
            table: "user_cosmetic_fragment_progress",
            type: "integer",
            nullable: false,
            defaultValue: 5);

        migrationBuilder.AddColumn<DateTime>(
            name: "UnlockedAtUtc",
            table: "user_cosmetic_fragment_progress",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE user_cosmetic_fragment_progress p
            SET "Collected" = p."Copies",
                "CosmeticItemId" = ci."Id",
                "Required" = COALESCE(ci."FragmentsRequired", 5)
            FROM cosmetic_items ci
            WHERE ci."FragmentLabel" = p."FragmentName"
               OR (p."FragmentName" = 'Comet Frame Fragment' AND ci."Key" = 'frame_comet')
               OR (p."FragmentName" = 'Nova Trail Fragment' AND ci."Key" = 'effect_nova_trail')
               OR (p."FragmentName" = 'Neon Number Burst Fragment' AND ci."Key" = 'effect_neon_number_burst');

            DELETE FROM user_cosmetic_fragment_progress WHERE "CosmeticItemId" IS NULL;

            UPDATE user_cosmetic_fragment_progress p
            SET "UnlockedAtUtc" = inv."UnlockedAt"
            FROM user_cosmetic_inventory inv
            WHERE inv."UserId" = p."UserId"
              AND inv."CosmeticItemId" = p."CosmeticItemId"
              AND inv."IsRevoked" = FALSE
              AND p."Collected" >= p."Required";
            """);

        migrationBuilder.DropIndex(
            name: "UX_user_cosmetic_fragment_progress_user_fragment",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropColumn(
            name: "FragmentName",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropColumn(
            name: "Copies",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.AlterColumn<int>(
            name: "CosmeticItemId",
            table: "user_cosmetic_fragment_progress",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        foreach (var (table, column, principalTable, principalColumn) in new[]
        {
            ("user_avatar_configs", "UserId", "UserProfiles", "UserId"),
            ("user_avatar_configs", "AccessoryId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "BackgroundId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "ClothingId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "EffectId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "EmojiId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "FrameId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "HairId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "LeaderboardDecorationId", "cosmetic_items", "Id"),
            ("user_avatar_configs", "SkinId", "cosmetic_items", "Id"),
            ("user_cosmetic_inventory", "CosmeticItemId", "cosmetic_items", "Id"),
        })
        {
            DropForeignKeyByColumns(migrationBuilder, table, column, principalTable, principalColumn);
        }

        DropPrimaryKeyByTable(migrationBuilder, "user_cosmetic_inventory");
        DropPrimaryKeyByTable(migrationBuilder, "user_avatar_configs");

        migrationBuilder.RenameTable(
            name: "user_cosmetic_inventory",
            newName: "user_cosmetics");

        migrationBuilder.RenameTable(
            name: "user_avatar_configs",
            newName: "user_avatar");

        migrationBuilder.RenameIndex(
            name: "UX_user_cosmetic_inventory_user_item",
            table: "user_cosmetics",
            newName: "UX_user_cosmetics_user_item");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetic_inventory_user_source",
            table: "user_cosmetics",
            newName: "IX_user_cosmetics_user_source");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetic_inventory_user",
            table: "user_cosmetics",
            newName: "IX_user_cosmetics_user");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetic_inventory_source_ref",
            table: "user_cosmetics",
            newName: "IX_user_cosmetics_source_ref");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetic_inventory_CosmeticItemId",
            table: "user_cosmetics",
            newName: "IX_user_cosmetics_CosmeticItemId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_SkinId",
            table: "user_avatar",
            newName: "IX_user_avatar_SkinId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_LeaderboardDecorationId",
            table: "user_avatar",
            newName: "IX_user_avatar_LeaderboardDecorationId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_HairId",
            table: "user_avatar",
            newName: "IX_user_avatar_HairId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_FrameId",
            table: "user_avatar",
            newName: "IX_user_avatar_FrameId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_EmojiId",
            table: "user_avatar",
            newName: "IX_user_avatar_EmojiId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_EffectId",
            table: "user_avatar",
            newName: "IX_user_avatar_EffectId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_ClothingId",
            table: "user_avatar",
            newName: "IX_user_avatar_ClothingId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_BackgroundId",
            table: "user_avatar",
            newName: "IX_user_avatar_BackgroundId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_configs_AccessoryId",
            table: "user_avatar",
            newName: "IX_user_avatar_AccessoryId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_user_cosmetics",
            table: "user_cosmetics",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_user_avatar",
            table: "user_avatar",
            column: "UserId");

        migrationBuilder.CreateTable(
            name: "cosmetics_idempotency_ledger",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                OperationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RequestJson = table.Column<string>(type: "jsonb", nullable: false),
                ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cosmetics_idempotency_ledger", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_cosmetic_fragment_progress_CosmeticItemId",
            table: "user_cosmetic_fragment_progress",
            column: "CosmeticItemId");

        migrationBuilder.CreateIndex(
            name: "UX_user_cosmetic_fragment_progress_user_item",
            table: "user_cosmetic_fragment_progress",
            columns: new[] { "UserId", "CosmeticItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_cosmetics_idempotency_ledger_user_idempotency",
            table: "cosmetics_idempotency_ledger",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_cosmetics_idempotency_ledger_user_operation",
            table: "cosmetics_idempotency_ledger",
            columns: new[] { "UserId", "OperationId" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_UserProfiles_UserId",
            table: "user_avatar",
            column: "UserId",
            principalTable: "UserProfiles",
            principalColumn: "UserId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_AccessoryId",
            table: "user_avatar",
            column: "AccessoryId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_BackgroundId",
            table: "user_avatar",
            column: "BackgroundId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_ClothingId",
            table: "user_avatar",
            column: "ClothingId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_EffectId",
            table: "user_avatar",
            column: "EffectId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_EmojiId",
            table: "user_avatar",
            column: "EmojiId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_FrameId",
            table: "user_avatar",
            column: "FrameId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_HairId",
            table: "user_avatar",
            column: "HairId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_LeaderboardDecorationId",
            table: "user_avatar",
            column: "LeaderboardDecorationId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_cosmetic_items_SkinId",
            table: "user_avatar",
            column: "SkinId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_cosmetic_fragment_progress_cosmetic_items_CosmeticItemId",
            table: "user_cosmetic_fragment_progress",
            column: "CosmeticItemId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_user_cosmetics_cosmetic_items_CosmeticItemId",
            table: "user_cosmetics",
            column: "CosmeticItemId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    private static void DropForeignKeyByColumns(
        MigrationBuilder migrationBuilder,
        string tableName,
        string columnName,
        string principalTableName,
        string principalColumnName)
    {
        migrationBuilder.Sql($$"""
            DO $$
            DECLARE constraint_name text;
            BEGIN
                SELECT c.conname
                INTO constraint_name
                FROM pg_constraint c
                JOIN pg_class rel ON rel.oid = c.conrelid
                JOIN pg_namespace relns ON relns.oid = rel.relnamespace
                JOIN pg_class refrel ON refrel.oid = c.confrelid
                JOIN pg_namespace refns ON refns.oid = refrel.relnamespace
                JOIN pg_attribute relattr ON relattr.attrelid = rel.oid AND relattr.attnum = ANY(c.conkey)
                JOIN pg_attribute refattr ON refattr.attrelid = refrel.oid AND refattr.attnum = ANY(c.confkey)
                WHERE c.contype = 'f'
                  AND relns.nspname = 'public'
                  AND refns.nspname = 'public'
                  AND rel.relname = '{{tableName}}'
                  AND refrel.relname = '{{principalTableName}}'
                  AND relattr.attname = '{{columnName}}'
                  AND refattr.attname = '{{principalColumnName}}'
                  AND cardinality(c.conkey) = 1
                  AND cardinality(c.confkey) = 1
                LIMIT 1;

                IF constraint_name IS NOT NULL THEN
                    EXECUTE format('ALTER TABLE %I DROP CONSTRAINT %I', '{{tableName}}', constraint_name);
                END IF;
            END $$;
            """);
    }

    private static void DropPrimaryKeyByTable(MigrationBuilder migrationBuilder, string tableName)
    {
        migrationBuilder.Sql($$"""
            DO $$
            DECLARE constraint_name text;
            BEGIN
                SELECT c.conname
                INTO constraint_name
                FROM pg_constraint c
                JOIN pg_class rel ON rel.oid = c.conrelid
                JOIN pg_namespace relns ON relns.oid = rel.relnamespace
                WHERE c.contype = 'p'
                  AND relns.nspname = 'public'
                  AND rel.relname = '{{tableName}}'
                LIMIT 1;

                IF constraint_name IS NOT NULL THEN
                    EXECUTE format('ALTER TABLE %I DROP CONSTRAINT %I', '{{tableName}}', constraint_name);
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_UserProfiles_UserId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_AccessoryId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_BackgroundId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_ClothingId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_EffectId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_EmojiId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_FrameId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_HairId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_LeaderboardDecorationId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_avatar_cosmetic_items_SkinId",
            table: "user_avatar");

        migrationBuilder.DropForeignKey(
            name: "FK_user_cosmetic_fragment_progress_cosmetic_items_CosmeticItemId",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropForeignKey(
            name: "FK_user_cosmetics_cosmetic_items_CosmeticItemId",
            table: "user_cosmetics");

        migrationBuilder.DropTable(
            name: "cosmetics_idempotency_ledger");

        migrationBuilder.DropIndex(
            name: "IX_user_cosmetic_fragment_progress_CosmeticItemId",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropIndex(
            name: "UX_user_cosmetic_fragment_progress_user_item",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropPrimaryKey(
            name: "PK_user_cosmetics",
            table: "user_cosmetics");

        migrationBuilder.DropPrimaryKey(
            name: "PK_user_avatar",
            table: "user_avatar");

        migrationBuilder.RenameTable(
            name: "user_cosmetics",
            newName: "user_cosmetic_inventory");

        migrationBuilder.RenameTable(
            name: "user_avatar",
            newName: "user_avatar_configs");

        migrationBuilder.RenameIndex(
            name: "UX_user_cosmetics_user_item",
            table: "user_cosmetic_inventory",
            newName: "UX_user_cosmetic_inventory_user_item");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetics_user_source",
            table: "user_cosmetic_inventory",
            newName: "IX_user_cosmetic_inventory_user_source");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetics_user",
            table: "user_cosmetic_inventory",
            newName: "IX_user_cosmetic_inventory_user");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetics_source_ref",
            table: "user_cosmetic_inventory",
            newName: "IX_user_cosmetic_inventory_source_ref");

        migrationBuilder.RenameIndex(
            name: "IX_user_cosmetics_CosmeticItemId",
            table: "user_cosmetic_inventory",
            newName: "IX_user_cosmetic_inventory_CosmeticItemId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_SkinId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_SkinId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_LeaderboardDecorationId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_LeaderboardDecorationId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_HairId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_HairId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_FrameId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_FrameId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_EmojiId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_EmojiId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_EffectId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_EffectId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_ClothingId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_ClothingId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_BackgroundId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_BackgroundId");

        migrationBuilder.RenameIndex(
            name: "IX_user_avatar_AccessoryId",
            table: "user_avatar_configs",
            newName: "IX_user_avatar_configs_AccessoryId");

        migrationBuilder.AddColumn<string>(
            name: "FragmentName",
            table: "user_cosmetic_fragment_progress",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "Copies",
            table: "user_cosmetic_fragment_progress",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("""
            UPDATE user_cosmetic_fragment_progress p
            SET "Copies" = p."Collected",
                "FragmentName" = COALESCE(ci."FragmentLabel", ci."Key")
            FROM cosmetic_items ci
            WHERE ci."Id" = p."CosmeticItemId";
            """);

        migrationBuilder.DropColumn(
            name: "Collected",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropColumn(
            name: "CosmeticItemId",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropColumn(
            name: "Required",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropColumn(
            name: "UnlockedAtUtc",
            table: "user_cosmetic_fragment_progress");

        migrationBuilder.DropColumn(
            name: "FragmentLabel",
            table: "cosmetic_items");

        migrationBuilder.DropColumn(
            name: "FragmentsRequired",
            table: "cosmetic_items");

        migrationBuilder.AddPrimaryKey(
            name: "PK_user_cosmetic_inventory",
            table: "user_cosmetic_inventory",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_user_avatar_configs",
            table: "user_avatar_configs",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "UX_user_cosmetic_fragment_progress_user_fragment",
            table: "user_cosmetic_fragment_progress",
            columns: new[] { "UserId", "FragmentName" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_UserProfiles_UserId",
            table: "user_avatar_configs",
            column: "UserId",
            principalTable: "UserProfiles",
            principalColumn: "UserId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_AccessoryId",
            table: "user_avatar_configs",
            column: "AccessoryId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_BackgroundId",
            table: "user_avatar_configs",
            column: "BackgroundId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_ClothingId",
            table: "user_avatar_configs",
            column: "ClothingId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_EffectId",
            table: "user_avatar_configs",
            column: "EffectId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_EmojiId",
            table: "user_avatar_configs",
            column: "EmojiId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_FrameId",
            table: "user_avatar_configs",
            column: "FrameId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_HairId",
            table: "user_avatar_configs",
            column: "HairId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_LeaderboardDecorationId",
            table: "user_avatar_configs",
            column: "LeaderboardDecorationId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_avatar_configs_cosmetic_items_SkinId",
            table: "user_avatar_configs",
            column: "SkinId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_user_cosmetic_inventory_cosmetic_items_CosmeticItemId",
            table: "user_cosmetic_inventory",
            column: "CosmeticItemId",
            principalTable: "cosmetic_items",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
