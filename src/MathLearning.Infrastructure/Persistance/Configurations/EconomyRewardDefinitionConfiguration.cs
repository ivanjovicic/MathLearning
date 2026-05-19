using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class EconomyRewardDefinitionConfiguration : IEntityTypeConfiguration<EconomyRewardDefinition>
{
    private static readonly DateTime SeedTimestampUtc = new(2026, 5, 19, 0, 0, 0, DateTimeKind.Utc);
    private const string AlwaysEligibleRuleJson = """
        {"type":"always"}
        """;
    private const string DailyGrantRuleJson = """
        {"coins":{"type":"const","value":20},"xp":{"type":"const","value":15}}
        """;
    private const string GenericOnboardingGrantRuleJson = """
        {"coins":{"type":"const","value":50},"xp":{"type":"const","value":0}}
        """;
    private const string GenericStarterGrantRuleJson = """
        {"coins":{"type":"const","value":25},"xp":{"type":"const","value":0}}
        """;
    private const string GenericWelcomeBackGrantRuleJson = """
        {"coins":{"type":"const","value":15},"xp":{"type":"const","value":10}}
        """;
    private const string LevelEligibilityRuleJson = """
        {"type":"compare","operator":"gte","left":{"type":"profile","field":"level"},"right":{"type":"capture","name":"threshold"}}
        """;
    private const string LevelGrantRuleJson = """
        {"coins":{"type":"clamp","value":{"type":"multiply","left":{"type":"capture","name":"threshold"},"right":{"type":"const","value":10}},"min":{"type":"const","value":10}},"xp":{"type":"const","value":0}}
        """;
    private const string StreakEligibilityRuleJson = """
        {"type":"compare","operator":"gte","left":{"type":"profile","field":"streak"},"right":{"type":"capture","name":"threshold"}}
        """;
    private const string StreakGrantRuleJson = """
        {"coins":{"type":"clamp","value":{"type":"multiply","left":{"type":"capture","name":"threshold"},"right":{"type":"const","value":5}},"min":{"type":"const","value":10},"max":{"type":"const","value":500}},"xp":{"type":"const","value":0}}
        """;

    public void Configure(EntityTypeBuilder<EconomyRewardDefinition> builder)
    {
        builder.ToTable("economy_reward_definitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RewardIdPattern)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.RewardType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.EligibilityRuleJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.GrantRuleJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.IneligibilityMessage)
            .IsRequired();

        builder.Property(x => x.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.RewardType, x.IsActive, x.Priority })
            .HasDatabaseName("IX_economy_reward_definitions_type_active_priority");

        builder.HasIndex(x => new { x.RewardType, x.RewardIdPattern })
            .IsUnique()
            .HasDatabaseName("UX_economy_reward_definitions_type_pattern");

        builder.HasData(
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("7B40D3BA-E74D-4E25-BD84-60D2D645A1C1"),
                RewardIdPattern = "^daily:(?<slug>.+)$",
                RewardType = "daily",
                Priority = 20,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = DailyGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = SeedTimestampUtc
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("2E3D6E31-3F8D-4D60-9266-3CBDB3A34729"),
                RewardIdPattern = "^generic:onboarding_bonus$",
                RewardType = "generic",
                Priority = 10,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = GenericOnboardingGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = SeedTimestampUtc
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("D4E88C31-56C0-494B-9611-271DB4F1DCD8"),
                RewardIdPattern = "^generic:starter_bonus$",
                RewardType = "generic",
                Priority = 10,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = GenericStarterGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = SeedTimestampUtc
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("E1F90A77-EEB8-4FD7-973E-E05449B7678A"),
                RewardIdPattern = "^generic:welcome_back$",
                RewardType = "generic",
                Priority = 10,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = GenericWelcomeBackGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = SeedTimestampUtc
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("D9D5E0D8-87FA-4819-BE4A-6285C2EF6FC7"),
                RewardIdPattern = "^level:(?<threshold>[1-9]\\d*)$",
                RewardType = "level",
                Priority = 30,
                EligibilityRuleJson = LevelEligibilityRuleJson,
                GrantRuleJson = LevelGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = SeedTimestampUtc
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("FA5D14D5-7931-4B57-B5D0-442AFC4BA26E"),
                RewardIdPattern = "^streak:(?<threshold>[1-9]\\d*)$",
                RewardType = "streak",
                Priority = 30,
                EligibilityRuleJson = StreakEligibilityRuleJson,
                GrantRuleJson = StreakGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = SeedTimestampUtc
            });
    }
}