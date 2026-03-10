using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class QuestionAuthoringQuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(x => x.PublishState)
            .HasMaxLength(32)
            .HasDefaultValue(QuestionPublishStates.Draft);

        builder.Property(x => x.PublishedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.PublishedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.HintFull)
            .HasColumnType("TEXT")
            .IsRequired(false);

        builder.HasIndex(x => x.PublishState)
            .HasDatabaseName("IX_Questions_PublishState");

        builder.HasIndex(x => x.CurrentDraftId)
            .HasDatabaseName("IX_Questions_CurrentDraftId");
    }
}

public sealed class QuestionDraftConfiguration : IEntityTypeConfiguration<QuestionDraft>
{
    public void Configure(EntityTypeBuilder<QuestionDraft> builder)
    {
        builder.ToTable("question_drafts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.NormalizedContentJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PublishState).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ValidationStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ChangeReason).HasMaxLength(500);
        builder.Property(x => x.AuthorUserId).HasMaxLength(450);
        builder.Property(x => x.EditorUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.QuestionId, x.DraftVersion })
            .IsUnique()
            .HasDatabaseName("UX_question_drafts_question_version");

        builder.HasIndex(x => x.ContentHash)
            .HasDatabaseName("IX_question_drafts_content_hash");

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LatestValidationResult)
            .WithMany()
            .HasForeignKey(x => x.LatestValidationResultId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class QuestionVersionConfiguration : IEntityTypeConfiguration<QuestionVersion>
{
    public void Configure(EntityTypeBuilder<QuestionVersion> builder)
    {
        builder.ToTable("question_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.NormalizedSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PublishState).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ChangeReason).HasMaxLength(500);
        builder.Property(x => x.AuthorUserId).HasMaxLength(450);
        builder.Property(x => x.EditorUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.PublishedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.QuestionId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_question_versions_question_version");

        builder.HasIndex(x => new { x.QuestionId, x.PublishedAtUtc })
            .HasDatabaseName("IX_question_versions_question_published_at");

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SourceDraft)
            .WithMany()
            .HasForeignKey(x => x.SourceDraftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PreviousVersion)
            .WithMany()
            .HasForeignKey(x => x.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QuestionValidationResultConfiguration : IEntityTypeConfiguration<QuestionValidationResult>
{
    public void Configure(EntityTypeBuilder<QuestionValidationResult> builder)
    {
        builder.ToTable("question_validation_results");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SummaryJson).HasColumnType("jsonb");
        builder.Property(x => x.PreviewPayloadJson).HasColumnType("jsonb");
        builder.Property(x => x.ValidatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.DraftId, x.ValidatedAtUtc })
            .HasDatabaseName("IX_question_validation_results_draft_validated");

        builder.HasOne(x => x.Draft)
            .WithMany()
            .HasForeignKey(x => x.DraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Issues)
            .WithOne(x => x.ValidationResult)
            .HasForeignKey(x => x.ValidationResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuestionValidationIssueConfiguration : IEntityTypeConfiguration<QuestionValidationIssue>
{
    public void Configure(EntityTypeBuilder<QuestionValidationIssue> builder)
    {
        builder.ToTable("question_validation_issues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RuleId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.FieldPath).HasMaxLength(256);
        builder.Property(x => x.Suggestion).HasMaxLength(1000);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        builder.HasIndex(x => new { x.ValidationResultId, x.Stage })
            .HasDatabaseName("IX_question_validation_issues_result_stage");
    }
}

public sealed class QuestionPreviewCacheConfiguration : IEntityTypeConfiguration<QuestionPreviewCache>
{
    public void Configure(EntityTypeBuilder<QuestionPreviewCache> builder)
    {
        builder.ToTable("question_preview_cache");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PreviewPayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.ContentHash)
            .HasDatabaseName("IX_question_preview_cache_content_hash");

        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("IX_question_preview_cache_expires");

        builder.HasOne(x => x.Draft)
            .WithMany()
            .HasForeignKey(x => x.DraftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuestionAuthoringAuditLogConfiguration : IEntityTypeConfiguration<QuestionAuthoringAuditLog>
{
    public void Configure(EntityTypeBuilder<QuestionAuthoringAuditLog> builder)
    {
        builder.ToTable("question_authoring_audit_log");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450);
        builder.Property(x => x.BeforeJson).HasColumnType("jsonb");
        builder.Property(x => x.AfterJson).HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.QuestionId, x.OccurredAtUtc })
            .HasDatabaseName("IX_question_authoring_audit_question_occurred");

        builder.HasIndex(x => new { x.DraftId, x.OccurredAtUtc })
            .HasDatabaseName("IX_question_authoring_audit_draft_occurred");

        builder.HasOne(x => x.Draft)
            .WithMany()
            .HasForeignKey(x => x.DraftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
