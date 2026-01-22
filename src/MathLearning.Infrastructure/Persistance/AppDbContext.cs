using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasDefaultValue("multiple_choice");
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Subtopic)
                  .WithMany()
                  .HasForeignKey(e => e.SubtopicId)
                  .OnDelete(DeleteBehavior.Restrict);
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
    }
}
