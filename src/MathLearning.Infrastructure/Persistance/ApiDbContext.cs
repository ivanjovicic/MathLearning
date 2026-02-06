using MathLearning.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Persistance;

public class ApiDbContext : IdentityDbContext<IdentityUser>
{
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserHint> UserHints => Set<UserHint>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ApplicationLog> ApplicationLogs => Set<ApplicationLog>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<QuestionStat> QuestionStats => Set<QuestionStat>();
    public DbSet<UserDailyStat> UserDailyStats => Set<UserDailyStat>();

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
            
            // 💡 Hint properties
            entity.Property(e => e.HintFormula).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintClue).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintDifficulty).HasDefaultValue(1);
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.SubtopicId)
                  .HasDatabaseName("IX_Questions_SubtopicId");
            entity.HasIndex(e => e.Difficulty)
                  .HasDatabaseName("IX_Questions_Difficulty");
            entity.HasIndex(e => new { e.SubtopicId, e.Difficulty })
                  .HasDatabaseName("IX_Questions_Subtopic_Difficulty");
        });

        builder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            
            // 🚀 Performance index za filtering correct answers
            entity.HasIndex(e => e.IsCorrect)
                  .HasDatabaseName("IX_Options_IsCorrect");
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            
            // 🚀 Unique index za Name (prevent duplicates)
            entity.HasIndex(e => e.Name)
                  .IsUnique()
                  .HasDatabaseName("UX_Categories_Name");
        });

        builder.Entity<Topic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Description).IsRequired(false);
            
            // 🚀 Unique index za Name
            entity.HasIndex(e => e.Name)
                  .IsUnique()
                  .HasDatabaseName("UX_Topics_Name");
        });

        builder.Entity<Subtopic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Topic)
                  .WithMany()
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.TopicId)
                  .HasDatabaseName("IX_Subtopics_TopicId");
            entity.HasIndex(e => new { e.TopicId, e.Name })
                  .IsUnique()
                  .HasDatabaseName("UX_Subtopics_Topic_Name");
        });

        builder.Entity<QuizSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // 🚀 Performance index za user sessions
            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_QuizSessions_UserId");
            entity.HasIndex(e => new { e.UserId, e.StartedAt })
                  .HasDatabaseName("IX_QuizSessions_User_Started");
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
            
            // 🔐 Unique index za zaštitu od duplikata
            entity.HasIndex(e => new { e.UserId, e.QuestionId, e.AnsweredAt })
                  .IsUnique()
                  .HasDatabaseName("UX_UserAnswers_NoDuplicate");
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_UserAnswers_UserId");
            entity.HasIndex(e => e.QuestionId)
                  .HasDatabaseName("IX_UserAnswers_QuestionId");
            entity.HasIndex(e => new { e.UserId, e.AnsweredAt })
                  .HasDatabaseName("IX_UserAnswers_User_Answered");
            entity.HasIndex(e => new { e.UserId, e.IsCorrect })
                  .HasDatabaseName("IX_UserAnswers_User_Correct");
        });

        builder.Entity<UserQuestionStat>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.QuestionId });
            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // 🚀 Performance index za sorting by last attempt
            entity.HasIndex(e => e.LastAttemptAt)
                  .HasDatabaseName("IX_UserQuestionStats_LastAttempt");
            entity.HasIndex(e => new { e.UserId, e.LastAttemptAt })
                  .HasDatabaseName("IX_UserQuestionStats_User_LastAttempt");
        });

        builder.Entity<UserFriend>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FriendId });
            
            // 🚀 Performance index za reverse lookup (friend -> users)
            entity.HasIndex(e => e.FriendId)
                  .HasDatabaseName("IX_UserFriends_FriendId");
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.Token)
                  .IsUnique()
                  .HasDatabaseName("UX_RefreshTokens_Token");
            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_RefreshTokens_UserId");
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt })
                  .HasDatabaseName("IX_RefreshTokens_User_Expires");
        });

        builder.Entity<UserHint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HintType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UsedAt).IsRequired();
            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_UserHints_UserId");
            entity.HasIndex(e => new { e.UserId, e.QuestionId })
                  .HasDatabaseName("IX_UserHints_User_Question");
            entity.HasIndex(e => e.UsedAt)
                  .HasDatabaseName("IX_UserHints_UsedAt");
        });

        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.Coins).HasDefaultValue(100);
            entity.Property(e => e.Level).HasDefaultValue(1);
            entity.Property(e => e.Xp).HasDefaultValue(0);
            entity.Property(e => e.Streak).HasDefaultValue(0);
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.UserId)
                  .IsUnique()
                  .HasDatabaseName("UX_UserProfiles_UserId");
            entity.HasIndex(e => e.Username)
                  .IsUnique()
                  .HasDatabaseName("UX_UserProfiles_Username");
            entity.HasIndex(e => e.DisplayName)
                  .HasDatabaseName("IX_UserProfiles_DisplayName");
        });

        builder.Entity<ApplicationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Level).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.Exception).HasColumnType("TEXT");
            entity.Property(e => e.Properties).HasColumnType("TEXT");
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.Timestamp)
                  .HasDatabaseName("IX_ApplicationLogs_Timestamp");
            entity.HasIndex(e => e.Level)
                  .HasDatabaseName("IX_ApplicationLogs_Level");
            entity.HasIndex(e => new { e.Level, e.Timestamp })
                  .HasDatabaseName("IX_ApplicationLogs_Level_Timestamp");
        });

        builder.Entity<UserSettings>(entity =>
        {
            entity.ToTable("user_settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Theme).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DailyNotificationTime).HasMaxLength(5);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId)
                  .IsUnique()
                  .HasDatabaseName("UX_UserSettings_UserId");
        });

        builder.Entity<QuestionStat>(entity =>
        {
            entity.ToTable("question_stats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ease).HasDefaultValue(1.3);
            entity.Property(e => e.SuccessStreak).HasDefaultValue(0);
            entity.Property(e => e.NextReview).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

            entity.HasIndex(e => new { e.UserId, e.QuestionId })
                  .IsUnique()
                  .HasDatabaseName("UX_QuestionStats_User_Question");

            entity.HasIndex(e => new { e.UserId, e.NextReview })
                  .HasDatabaseName("IX_QuestionStats_User_NextReview");

            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserDailyStat>(entity =>
        {
            entity.ToTable("user_daily_stats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Day).HasColumnType("date");

            entity.HasIndex(e => new { e.UserId, e.Day })
                  .IsUnique()
                  .HasDatabaseName("UX_UserDailyStats_User_Day");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_UserDailyStats_UserId");
        });
    }
}
