using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance.Configurations;

namespace MathLearning.Admin.Data;

public class AdminDbContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> Options => Set<QuestionOption>();
    public DbSet<QuestionStep> QuestionSteps => Set<QuestionStep>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Subtopic> Subtopics => Set<Subtopic>();
    public DbSet<QuestionTranslation> QuestionTranslations => Set<QuestionTranslation>();
    public DbSet<BugReport> BugReports => Set<BugReport>();
    public DbSet<QuestionDraft> QuestionDrafts => Set<QuestionDraft>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<QuestionValidationResult> QuestionValidationResults => Set<QuestionValidationResult>();
    public DbSet<QuestionValidationIssue> QuestionValidationIssues => Set<QuestionValidationIssue>();
    public DbSet<QuestionPreviewCache> QuestionPreviewCaches => Set<QuestionPreviewCache>();
    public DbSet<QuestionAuthoringAuditLog> QuestionAuthoringAuditLogs => Set<QuestionAuthoringAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Topic>().ToTable("Topics");
        builder.Entity<Subtopic>().ToTable("Subtopics");
        builder.Entity<QuestionTranslation>().ToTable("QuestionTranslations");
        builder.Entity<QuestionStep>().ToTable("QuestionSteps");
        builder.Entity<QuestionStepTranslation>().ToTable("QuestionStepTranslations");
        builder.Entity<OptionTranslation>().ToTable("OptionTranslations");
        builder.Entity<BugReport>().ToTable("bug_reports");
        builder.ApplyConfiguration(new QuestionAuthoringQuestionConfiguration());
        builder.ApplyConfiguration(new QuestionDraftConfiguration());
        builder.ApplyConfiguration(new QuestionVersionConfiguration());
        builder.ApplyConfiguration(new QuestionValidationResultConfiguration());
        builder.ApplyConfiguration(new QuestionValidationIssueConfiguration());
        builder.ApplyConfiguration(new QuestionPreviewCacheConfiguration());
        builder.ApplyConfiguration(new QuestionAuthoringAuditLogConfiguration());

        builder.Entity<Question>(entity =>
        {
            entity.Property(e => e.TextFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ExplanationFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.TextRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ExplanationRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SemanticsAltText).HasMaxLength(1000);
            entity.Property(e => e.HintFull).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.PublishState).HasMaxLength(32).HasDefaultValue(QuestionPublishStates.Draft);
            entity.Property(e => e.PublishedByUserId).HasMaxLength(450).IsRequired(false);
            entity.Property(e => e.PublishedAtUtc).HasColumnType("timestamp with time zone").IsRequired(false);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256).IsRequired(false);
            entity.Property(e => e.PreviousSnapshotJson).HasColumnType("jsonb").IsRequired(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).IsRequired(false);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => e.PublishState);
            entity.HasIndex(e => e.CurrentDraftId);

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subtopic)
                .WithMany()
                .HasForeignKey(e => e.SubtopicId);
            entity.HasOne<QuestionOption>()
                .WithMany()
                .HasForeignKey(e => e.CorrectOptionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Options)
                .WithOne();
            entity.HasMany(e => e.Steps)
                .WithOne(x => x.Question)
                .HasForeignKey(x => x.QuestionId);
            entity.Property<uint>("xmin")
                .HasColumnName("xmin")
                .IsRowVersion();
        });

        builder.Entity<QuestionOption>(entity =>
        {
            entity.Property(e => e.TextFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.RenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SemanticsAltText).HasMaxLength(500);
            entity.Property(e => e.Order).HasDefaultValue(0).IsRequired();
        });

        builder.Entity<QuestionStep>(entity =>
        {
            entity.Property(e => e.TextFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.TextRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SemanticsAltText).HasMaxLength(500);
            entity.Property(e => e.Hint).HasColumnType("TEXT").IsRequired(false);

            entity.HasMany(e => e.Translations)
                .WithOne(t => t.QuestionStep)
                .HasForeignKey(t => t.QuestionStepId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
