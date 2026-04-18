using MathLearning.Domain.Entities;
using MathLearning.Domain.Primitives;
using MathLearning.Infrastructure.Persistance.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MathLearning.Infrastructure.Persistance;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> Options => Set<QuestionOption>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Subtopic> Subtopics => Set<Subtopic>();
    public DbSet<QuizSession> QuizSessions => Set<QuizSession>();
    public DbSet<UserAnswer> UserAnswers => Set<UserAnswer>();
    public DbSet<UserQuestionStat> UserQuestionStats => Set<UserQuestionStat>();
    public DbSet<UserFriend> UserFriends => Set<UserFriend>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Collect domain events from all tracked entities
        var domainEvents = ChangeTracker.Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        // Convert domain events to outbox messages (in same transaction)
        foreach (var ev in domainEvents)
        {
            Outbox.Add(new OutboxMessage
            {
                Id = ev.Id,
                OccurredUtc = ev.OccurredUtc,
                Type = ev.GetType().AssemblyQualifiedName!,
                PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
            });
        }

        var result = await base.SaveChangesAsync(ct);

        // Clear domain events after successful commit
        foreach (var entry in ChangeTracker.Entries<Entity>())
            entry.Entity.ClearDomainEvents();

        return result;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasDefaultValue("multiple_choice");
            entity.Property(e => e.UpdatedBy).HasMaxLength(256).IsRequired(false);
            entity.Property(e => e.PreviousSnapshotJson).HasColumnType("jsonb").IsRequired(false);
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Subtopic)
                  .WithMany()
                  .HasForeignKey(e => e.SubtopicId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<QuestionOption>()
                  .WithMany()
                  .HasForeignKey(e => e.CorrectOptionId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Options)
                  .WithOne()
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });

        builder.Entity<Topic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Description).IsRequired(false);
        });

        builder.Entity<Subtopic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Topic)
                  .WithMany()
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuizSession>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        builder.Entity<UserAnswer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Answer).IsRequired();
            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.QuizSession)
                  .WithMany()
                  .HasForeignKey(e => e.QuizSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserQuestionStat>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.QuestionId });
            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserFriend>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FriendId });
            
            // Napomena: Pošto nemaš User entitet u domain modelu, 
            // ne mogu da dodam FK ka Users tabeli iz AdminDbContext.
            // Moraćeš ručno da dodaš FK constraint u migraciji ili
            // da kreiraš User entitet u Domain projektu.
        });

        builder.Entity<UserProgress>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Coins).IsRequired();
            entity.Property(e => e.TotalXp).IsRequired();
            entity.OwnsOne(e => e.Streak, streak =>
            {
                streak.Property(s => s.CurrentStreak).IsRequired();
                streak.Property(s => s.LastActivityDate).IsRequired();
                streak.Property(s => s.FreezeCount).IsRequired();
            });
            entity.Ignore(e => e.DomainEvents);
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.HasIndex(e => new { e.ProcessedUtc, e.OccurredUtc });
        });
    }
}
