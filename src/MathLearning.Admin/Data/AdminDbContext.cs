using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MathLearning.Domain.Entities;

namespace MathLearning.Admin.Data;

public class AdminDbContext : IdentityDbContext<IdentityUser>
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> Options => Set<QuestionOption>();
    public DbSet<QuestionStep> QuestionSteps => Set<QuestionStep>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Subtopic> Subtopics => Set<Subtopic>();
    public DbSet<QuestionTranslation> QuestionTranslations => Set<QuestionTranslation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Topic>().ToTable("Topic");
        builder.Entity<Subtopic>().ToTable("Subtopic");
        builder.Entity<QuestionTranslation>().ToTable("QuestionTranslation");
        builder.Entity<QuestionStep>().ToTable("QuestionStep");
        builder.Entity<QuestionStepTranslation>().ToTable("QuestionStepTranslation");
        builder.Entity<OptionTranslation>().ToTable("OptionTranslation");

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

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId);
            entity.HasOne(e => e.Subtopic)
                .WithMany()
                .HasForeignKey(e => e.SubtopicId);
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
