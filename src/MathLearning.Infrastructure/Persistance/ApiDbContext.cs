using MathLearning.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MathLearning.Infrastructure.Persistance.Models;

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
    public DbSet<BugReport> BugReports => Set<BugReport>();
    public DbSet<QuestionTranslation> QuestionTranslations => Set<QuestionTranslation>();
    public DbSet<OptionTranslation> OptionTranslations => Set<OptionTranslation>();
    public DbSet<QuestionStep> QuestionSteps => Set<QuestionStep>();
    public DbSet<QuestionStepTranslation> QuestionStepTranslations => Set<QuestionStepTranslation>();
    public DbSet<QuestionDraft> QuestionDrafts => Set<QuestionDraft>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<QuestionValidationResult> QuestionValidationResults => Set<QuestionValidationResult>();
    public DbSet<QuestionValidationIssue> QuestionValidationIssues => Set<QuestionValidationIssue>();
    public DbSet<QuestionPreviewCache> QuestionPreviewCaches => Set<QuestionPreviewCache>();
    public DbSet<QuestionAuthoringAuditLog> QuestionAuthoringAuditLogs => Set<QuestionAuthoringAuditLog>();
    public DbSet<StepExplanationTemplate> StepExplanationTemplates => Set<StepExplanationTemplate>();
    public DbSet<StepExplanationCacheEntry> StepExplanationCacheEntries => Set<StepExplanationCacheEntry>();
    public DbSet<MathFormulaReferenceEntity> MathFormulaReferences => Set<MathFormulaReferenceEntity>();
    public DbSet<CommonMistakePattern> CommonMistakePatterns => Set<CommonMistakePattern>();
    public DbSet<MathTransformationRule> MathTransformationRules => Set<MathTransformationRule>();
    public DbSet<UserAnswerAudit> UserAnswerAudits => Set<UserAnswerAudit>();
    public DbSet<UserQuestionAttempt> UserQuestionAttempts => Set<UserQuestionAttempt>();
    public DbSet<UserXpEvent> UserXpEvents => Set<UserXpEvent>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolScoreAggregate> SchoolScoreAggregates => Set<SchoolScoreAggregate>();
    public DbSet<SchoolRankHistory> SchoolRankHistories => Set<SchoolRankHistory>();
    public DbSet<CompetitionSeason> CompetitionSeasons => Set<CompetitionSeason>();
    public DbSet<XpCheatLog> XpCheatLogs => Set<XpCheatLog>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<UserLearningProfile> UserLearningProfiles => Set<UserLearningProfile>();
    public DbSet<UserTopicMastery> UserTopicMasteries => Set<UserTopicMastery>();
    public DbSet<UserQuestionHistory> UserQuestionHistories => Set<UserQuestionHistory>();
    public DbSet<ReviewSchedule> ReviewSchedules => Set<ReviewSchedule>();
    public DbSet<AdaptiveSession> AdaptiveSessions => Set<AdaptiveSession>();
    public DbSet<AdaptiveSessionItem> AdaptiveSessionItems => Set<AdaptiveSessionItem>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<UserTopicStat> UserTopicStats => Set<UserTopicStat>();
    public DbSet<UserSubtopicStat> UserSubtopicStats => Set<UserSubtopicStat>();
    public DbSet<UserWeakness> UserWeaknesses => Set<UserWeakness>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<PracticeSessionItem> PracticeSessionItems => Set<PracticeSessionItem>();
    public DbSet<MasteryState> MasteryStates => Set<MasteryState>();
    public DbSet<AnswerPatternDetectionLog> AnswerPatternDetectionLogs => Set<AnswerPatternDetectionLog>();
    public DbSet<DesignTokenVersion> DesignTokenVersions => Set<DesignTokenVersion>();
    public DbSet<DesignTokenSet> DesignTokenSets => Set<DesignTokenSet>();
    public DbSet<DesignToken> DesignTokens => Set<DesignToken>();
    public DbSet<DesignTokenAuditLog> DesignTokenAuditLogs => Set<DesignTokenAuditLog>();
    public DbSet<SyncDevice> SyncDevices => Set<SyncDevice>();
    public DbSet<SyncEventLog> SyncEventLogs => Set<SyncEventLog>();
    public DbSet<DeviceSyncState> DeviceSyncStates => Set<DeviceSyncState>();
    public DbSet<ServerSyncEvent> ServerSyncEvents => Set<ServerSyncEvent>();
    public DbSet<SyncDeadLetter> SyncDeadLetters => Set<SyncDeadLetter>();

    // 🎨 Cosmetic system
    public DbSet<CosmeticItem> CosmeticItems => Set<CosmeticItem>();
    public DbSet<CosmeticSeason> CosmeticSeasons => Set<CosmeticSeason>();
    public DbSet<UserCosmeticInventory> UserCosmeticInventories => Set<UserCosmeticInventory>();
    public DbSet<UserAvatarConfig> UserAvatarConfigs => Set<UserAvatarConfig>();
    public DbSet<CosmeticRewardRule> CosmeticRewardRules => Set<CosmeticRewardRule>();
    public DbSet<CosmeticRewardClaim> CosmeticRewardClaims => Set<CosmeticRewardClaim>();
    public DbSet<SeasonRewardTrackEntry> SeasonRewardTrackEntries => Set<SeasonRewardTrackEntry>();
    public DbSet<UserAppearanceProjection> UserAppearanceProjections => Set<UserAppearanceProjection>();
    public DbSet<CosmeticTelemetryEvent> CosmeticTelemetryEvents => Set<CosmeticTelemetryEvent>();
    public DbSet<CosmeticAuditLog> CosmeticAuditLogs => Set<CosmeticAuditLog>();
    public DbSet<LeaderboardSnapshot> LeaderboardSnapshots => Set<LeaderboardSnapshot>();
    public DbSet<UserQuizSummary> UserQuizSummaries => Set<UserQuizSummary>();
    public DbSet<UserRewardState> UserRewardStates => Set<UserRewardState>();

    // Outbox for background processing (OutboxProcessor uses AppDbContext, but the schema is shared).
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApiDbContext).Assembly);

        builder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasDefaultValue("multiple_choice");
            entity.Ignore(e => e.CorrectOptionId);
            entity.Property(e => e.TextFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ExplanationFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.TextRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ExplanationRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SemanticsAltText).HasMaxLength(1000);
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
            entity.HasMany(e => e.Options)
                  .WithOne()
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Translations)
                  .WithOne(t => t.Question)
                  .HasForeignKey(t => t.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Steps)
                  .WithOne(s => s.Question)
                  .HasForeignKey(s => s.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // 💡 Hint properties
            entity.Property(e => e.HintFormula).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintClue).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintFull).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintDifficulty).HasDefaultValue(1);
            
            // 🚀 Performance indexes
            entity.HasIndex(e => e.SubtopicId)
                  .HasDatabaseName("IX_Questions_SubtopicId");
            entity.HasIndex(e => e.Difficulty)
                  .HasDatabaseName("IX_Questions_Difficulty");
            entity.HasIndex(e => new { e.SubtopicId, e.Difficulty })
                  .HasDatabaseName("IX_Questions_Subtopic_Difficulty");
            entity.Property<uint>("xmin")
                  .HasColumnName("xmin")
                  .IsRowVersion();
        });

        builder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.TextFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.RenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SemanticsAltText).HasMaxLength(500);
            entity.Property(e => e.Order).HasDefaultValue(0).IsRequired();
            entity.HasMany(e => e.Translations)
                  .WithOne(t => t.Option)
                  .HasForeignKey(t => t.OptionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
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
            entity.Property(e => e.DeviceId).HasMaxLength(128);
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
            entity.HasIndex(e => e.SyncOperationId)
                  .IsUnique()
                  .HasDatabaseName("UX_UserAnswers_SyncOperationId");
            entity.HasIndex(e => new { e.UserId, e.DeviceId, e.ClientSequence })
                  .IsUnique()
                  .HasDatabaseName("UX_UserAnswers_User_Device_Sequence");
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
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.Coins).HasDefaultValue(100);
            entity.Property(e => e.Level).HasDefaultValue(1);
            entity.Property(e => e.Xp).HasDefaultValue(0);
            entity.Property(e => e.Streak).HasDefaultValue(0);
            entity.Property(e => e.StreakFreezeCount).HasDefaultValue(0);
            entity.Property(e => e.LastStreakDay).HasColumnType("date");
            entity.Property(e => e.LastActivityDay).HasColumnType("date");
            
            // 📈 Time-based XP tracking
            entity.Property(e => e.DailyXp).HasDefaultValue(0);
            entity.Property(e => e.WeeklyXp).HasDefaultValue(0);
            entity.Property(e => e.MonthlyXp).HasDefaultValue(0);
            entity.Property(e => e.LastXpResetDate).HasColumnType("timestamp with time zone");
            
            // 🏆 Leaderboard settings
            entity.Property(e => e.LeaderboardOptIn).HasDefaultValue(true);
            entity.Property(e => e.SchoolId).IsRequired(false);
            entity.Property(e => e.FacultyId).IsRequired(false);

            // 1:1 to Identity user (AspNetUsers). UserProfile.UserId is both PK and FK.
            entity.HasOne<IdentityUser>()
                  .WithOne()
                  .HasForeignKey<UserProfile>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
             
            // 🚀 Performance indexes
            entity.HasIndex(e => e.Username)
                  .IsUnique()
                  .HasDatabaseName("UX_UserProfiles_Username");
            entity.HasIndex(e => e.DisplayName)
                  .HasDatabaseName("IX_UserProfiles_DisplayName");
            
            // 🏆 Leaderboard performance indexes
            entity.HasIndex(e => new { e.LeaderboardOptIn, e.Xp })
                  .HasDatabaseName("IX_UserProfiles_Leaderboard_TotalXp");
            entity.HasIndex(e => new { e.LeaderboardOptIn, e.WeeklyXp })
                  .HasDatabaseName("IX_UserProfiles_Leaderboard_WeeklyXp");
            entity.HasIndex(e => new { e.LeaderboardOptIn, e.MonthlyXp })
                  .HasDatabaseName("IX_UserProfiles_Leaderboard_MonthlyXp");
            entity.HasIndex(e => new { e.LeaderboardOptIn, e.DailyXp })
                  .HasDatabaseName("IX_UserProfiles_Leaderboard_DailyXp");
            entity.HasIndex(e => new { e.SchoolId, e.LeaderboardOptIn })
                  .HasDatabaseName("IX_UserProfiles_School_Leaderboard");
            entity.HasIndex(e => new { e.FacultyId, e.LeaderboardOptIn })
                  .HasDatabaseName("IX_UserProfiles_Faculty_Leaderboard");
        });

        builder.Entity<UserAnswerAudit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ClientId).HasMaxLength(128);
            entity.Property(e => e.Answer).IsRequired();
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(64);
            entity.Property(e => e.AnsweredAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_UserAnswerAudits_UserId");
            entity.HasIndex(e => e.QuestionId)
                  .HasDatabaseName("IX_UserAnswerAudits_QuestionId");
            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("IX_UserAnswerAudits_CreatedAt");
            entity.HasIndex(e => new { e.UserId, e.QuestionId, e.CreatedAt })
                  .HasDatabaseName("IX_UserAnswerAudits_User_Question_CreatedAt");
            entity.HasIndex(e => new { e.UserId, e.QuestionId, e.IsFirstTimeCorrect })
                  .IsUnique()
                  .HasFilter("\"IsFirstTimeCorrect\" = true")
                  .HasDatabaseName("UX_UserAnswerAudits_FirstCorrect_PerQuestion");
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

        builder.Entity<BugReport>(entity =>
        {
            entity.ToTable("bug_reports");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.UsernameSnapshot).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Screen).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.StepsToReproduce).HasMaxLength(2000);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Locale).IsRequired().HasMaxLength(10);
            entity.Property(e => e.AppVersion).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ScreenshotUrl).HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Assignee).HasMaxLength(256);

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_BugReports_UserId");
            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("IX_BugReports_CreatedAt");
            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_BugReports_Status");
            entity.HasIndex(e => e.Severity)
                  .HasDatabaseName("IX_BugReports_Severity");
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                  .HasDatabaseName("IX_BugReports_Status_CreatedAt");
            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                  .HasDatabaseName("IX_BugReports_User_CreatedAt");
        });

        builder.Entity<QuestionTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Lang).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Explanation).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintFormula).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.HintClue).HasColumnType("TEXT").IsRequired(false);

            entity.HasIndex(e => new { e.QuestionId, e.Lang })
                  .IsUnique()
                  .HasDatabaseName("UX_QuestionTranslations_Question_Lang");

            entity.HasIndex(e => e.Lang)
                  .HasDatabaseName("IX_QuestionTranslations_Lang");
        });

        builder.Entity<OptionTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Lang).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Text).IsRequired();

            entity.HasIndex(e => new { e.OptionId, e.Lang })
                  .IsUnique()
                  .HasDatabaseName("UX_OptionTranslations_Option_Lang");
        });

        builder.Entity<QuestionStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Hint).HasColumnType("TEXT").IsRequired(false);
            entity.Property(e => e.Highlight).HasDefaultValue(false);
            entity.Property(e => e.StepIndex).IsRequired();
            entity.Property(e => e.TextFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.TextRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.HintRenderMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SemanticsAltText).HasMaxLength(500);

            entity.HasMany(e => e.Translations)
                  .WithOne(t => t.QuestionStep)
                  .HasForeignKey(t => t.QuestionStepId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.QuestionId, e.StepIndex })
                  .HasDatabaseName("IX_QuestionSteps_Question_Index");
        });

        builder.Entity<QuestionStepTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Lang).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Hint).HasColumnType("TEXT").IsRequired(false);

            entity.HasIndex(e => new { e.QuestionStepId, e.Lang })
                  .IsUnique()
                  .HasDatabaseName("UX_QuestionStepTranslations_Step_Lang");
        });

        builder.Entity<StepExplanationTemplate>(entity =>
        {
            entity.ToTable("step_explanation_template");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
            entity.Property(e => e.StepType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TemplateText).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.HintTemplate).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.RuleKey, e.Language, e.StepType })
                  .IsUnique()
                  .HasDatabaseName("UX_step_explanation_template_rule_lang_step");
        });

        builder.Entity<StepExplanationCacheEntry>(entity =>
        {
            entity.ToTable("step_explanation_cache");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProblemHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Difficulty).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PayloadJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.LastAccessedAt).IsRequired();

            entity.HasIndex(e => new { e.ProblemHash, e.Grade, e.Difficulty })
                  .IsUnique()
                  .HasDatabaseName("UX_step_explanation_cache_problem_grade_difficulty");
            entity.HasIndex(e => e.ExpiresAt)
                  .HasDatabaseName("IX_step_explanation_cache_expires_at");
        });

        builder.Entity<MathFormulaReferenceEntity>(entity =>
        {
            entity.ToTable("math_formula_reference");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Latex).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.MathMl).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Description).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Name)
                  .HasDatabaseName("IX_math_formula_reference_name");
        });

        builder.Entity<CommonMistakePattern>(entity =>
        {
            entity.ToTable("common_mistake_patterns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Topic).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Subtopic).HasMaxLength(100);
            entity.Property(e => e.MistakeType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PatternKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Remediation).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.Topic, e.Subtopic, e.MistakeType })
                  .HasDatabaseName("IX_common_mistake_patterns_topic_subtopic_type");
            entity.HasIndex(e => new { e.MistakeType, e.Priority })
                  .HasDatabaseName("IX_common_mistake_patterns_type_priority");
        });

        builder.Entity<MathTransformationRule>(entity =>
        {
            entity.ToTable("math_transformation_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.ExpressionPattern).HasColumnType("TEXT");
            entity.Property(e => e.StepType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExampleLatex).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.IsActive, e.StepType })
                  .HasDatabaseName("IX_math_transformation_rules_active_step");
        });

        builder.Entity<UserLearningProfile>(entity =>
        {
            entity.ToTable("user_learning_profiles");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PreferredDifficulty)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(AdaptiveDifficultyLevels.Medium);
            entity.Property(e => e.RollingWindowSize).HasDefaultValue(20);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.PreferredDifficulty })
                  .HasDatabaseName("IX_UserLearningProfiles_User_Difficulty");
        });

        builder.Entity<UserTopicMastery>(entity =>
        {
            entity.ToTable("user_topic_mastery");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.MasteryScore).HasDefaultValue(0d);
            entity.Property(e => e.DifficultyLevel)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(AdaptiveDifficultyLevels.Medium);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.TopicId })
                  .IsUnique()
                  .HasDatabaseName("UX_UserTopicMastery_User_Topic");

            entity.HasIndex(e => new { e.UserId, e.DifficultyLevel })
                  .HasDatabaseName("IX_UserTopicMastery_User_Difficulty");

            entity.HasIndex(e => new { e.UserId, e.IsWeak })
                  .HasDatabaseName("IX_UserTopicMastery_User_IsWeak");

            entity.HasOne(e => e.Topic)
                  .WithMany()
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserQuestionHistory>(entity =>
        {
            entity.ToTable("user_question_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.DifficultyLevel)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(AdaptiveDifficultyLevels.Medium);
            entity.Property(e => e.AnsweredAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.TopicId })
                  .HasDatabaseName("IX_UserQuestionHistory_User_Topic");

            entity.HasIndex(e => new { e.UserId, e.AnsweredAt })
                  .HasDatabaseName("IX_UserQuestionHistory_User_AnsweredAt");

            entity.HasIndex(e => new { e.UserId, e.QuestionId })
                  .HasDatabaseName("IX_UserQuestionHistory_User_Question");

            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Topic)
                  .WithMany()
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Subtopic)
                  .WithMany()
                  .HasForeignKey(e => e.SubtopicId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReviewSchedule>(entity =>
        {
            entity.ToTable("review_schedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.EasinessFactor).HasDefaultValue(2.5d);
            entity.Property(e => e.IntervalDays).HasDefaultValue(1);
            entity.Property(e => e.DueAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.QuestionId })
                  .IsUnique()
                  .HasDatabaseName("UX_ReviewSchedules_User_Question");

            entity.HasIndex(e => new { e.UserId, e.DueAt })
                  .HasDatabaseName("IX_ReviewSchedules_User_DueAt");

            entity.HasIndex(e => new { e.UserId, e.TopicId })
                  .HasDatabaseName("IX_ReviewSchedules_User_Topic");

            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Topic)
                  .WithMany()
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AdaptiveSession>(entity =>
        {
            entity.ToTable("adaptive_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ProfileDifficulty)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(AdaptiveDifficultyLevels.Medium);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                  .HasDatabaseName("IX_AdaptiveSessions_User_CreatedAt");

            entity.HasMany(e => e.Items)
                  .WithOne(i => i.AdaptiveSession)
                  .HasForeignKey(i => i.AdaptiveSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdaptiveSessionItem>(entity =>
        {
            entity.ToTable("adaptive_session_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DifficultyLevel)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(AdaptiveDifficultyLevels.Medium);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.AdaptiveSessionId, e.Sequence })
                  .IsUnique()
                  .HasDatabaseName("UX_AdaptiveSessionItems_Session_Sequence");

            entity.HasIndex(e => new { e.TopicId, e.DifficultyLevel })
                  .HasDatabaseName("IX_AdaptiveSessionItems_Topic_Difficulty");

            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Topic)
                  .WithMany()
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Subtopic)
                  .WithMany()
                  .HasForeignKey(e => e.SubtopicId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuizAttempt>(entity =>
        {
            entity.ToTable("quiz_attempt");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.QuizId).IsRequired();
            entity.Property(e => e.TimeSpentMs).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                  .HasDatabaseName("IX_quiz_attempt_user_created_at");
            entity.HasIndex(e => new { e.UserId, e.TopicId, e.CreatedAt })
                  .HasDatabaseName("IX_quiz_attempt_user_topic_created_at");
            entity.HasIndex(e => new { e.UserId, e.SubtopicId, e.CreatedAt })
                  .HasDatabaseName("IX_quiz_attempt_user_subtopic_created_at");
            entity.HasIndex(e => new { e.UserId, e.Correct })
                  .HasDatabaseName("IX_quiz_attempt_user_correct");
        });

        builder.Entity<UserTopicStat>(entity =>
        {
            entity.ToTable("user_topic_stats");
            entity.HasKey(e => new { e.UserId, e.TopicId });
            entity.Property(e => e.Accuracy).HasColumnType("numeric(5,4)");
            entity.Property(e => e.WeaknessScore).HasColumnType("numeric(8,4)");
            entity.Property(e => e.LastAttempt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.TopicId })
                  .IsUnique()
                  .HasDatabaseName("UX_user_topic_stats_user_topic");
            entity.HasIndex(e => new { e.UserId, e.WeaknessScore })
                  .HasDatabaseName("IX_user_topic_stats_user_weakness_score");
            entity.HasIndex(e => new { e.UserId, e.Accuracy })
                  .HasDatabaseName("IX_user_topic_stats_user_accuracy");
        });

        builder.Entity<UserSubtopicStat>(entity =>
        {
            entity.ToTable("user_subtopic_stats");
            entity.HasKey(e => new { e.UserId, e.SubtopicId });
            entity.Property(e => e.Accuracy).HasColumnType("numeric(5,4)");
            entity.Property(e => e.WeaknessScore).HasColumnType("numeric(8,4)");
            entity.Property(e => e.LastAttempt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.SubtopicId })
                  .IsUnique()
                  .HasDatabaseName("UX_user_subtopic_stats_user_subtopic");
            entity.HasIndex(e => new { e.UserId, e.WeaknessScore })
                  .HasDatabaseName("IX_user_subtopic_stats_user_weakness_score");
            entity.HasIndex(e => new { e.UserId, e.Accuracy })
                  .HasDatabaseName("IX_user_subtopic_stats_user_accuracy");
        });

        builder.Entity<UserWeakness>(entity =>
        {
            entity.ToTable("user_weakness");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WeaknessLevel).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Confidence).HasColumnType("numeric(5,4)");
            entity.Property(e => e.RecommendedPractice).HasColumnType("jsonb");
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.WeaknessLevel })
                  .HasDatabaseName("IX_user_weakness_user_level");
            entity.HasIndex(e => new { e.UserId, e.TopicId })
                  .HasDatabaseName("IX_user_weakness_user_topic");
            entity.HasIndex(e => new { e.UserId, e.SubtopicId })
                  .HasDatabaseName("IX_user_weakness_user_subtopic");
            entity.HasIndex(e => new { e.UserId, e.UpdatedAt })
                  .HasDatabaseName("IX_user_weakness_user_updated");
            entity.HasIndex(e => new { e.UserId, e.TopicId, e.SubtopicId })
                  .IsUnique()
                  .HasDatabaseName("UX_user_weakness_user_topic_subtopic");
        });

        builder.Entity<PracticeSession>(entity =>
        {
            entity.ToTable("practice_session");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SkillNodeId).HasMaxLength(128);
            entity.Property(e => e.Status)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasDefaultValue(PracticeSessionStatuses.Active);
            entity.Property(e => e.TargetQuestions).HasDefaultValue(10);
            entity.Property(e => e.AnsweredQuestions).HasDefaultValue(0);
            entity.Property(e => e.CorrectAnswers).HasDefaultValue(0);
            entity.Property(e => e.XpEarned).HasDefaultValue(0);
            entity.Property(e => e.RecommendedDifficulty)
                  .IsRequired()
                  .HasMaxLength(16)
                  .HasDefaultValue(PracticeDifficulties.Medium);
            entity.Property(e => e.InitialMastery).HasColumnType("numeric(5,4)");
            entity.Property(e => e.FinalMastery).HasColumnType("numeric(5,4)");

            entity.HasIndex(e => new { e.UserId, e.StartedAt })
                  .HasDatabaseName("IX_practice_session_user_started_at");
            entity.HasIndex(e => new { e.UserId, e.Status })
                  .HasDatabaseName("IX_practice_session_user_status");
            entity.HasIndex(e => new { e.UserId, e.TopicId })
                  .HasDatabaseName("IX_practice_session_user_topic");
            entity.HasIndex(e => new { e.UserId, e.SubtopicId })
                  .HasDatabaseName("IX_practice_session_user_subtopic");
        });

        builder.Entity<PracticeSessionItem>(entity =>
        {
            entity.ToTable("practice_session_item");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Difficulty)
                  .IsRequired()
                  .HasMaxLength(16)
                  .HasDefaultValue(PracticeDifficulties.Medium);
            entity.Property(e => e.AttemptNumber).HasDefaultValue(1);
            entity.Property(e => e.BktPrior).HasColumnType("numeric(5,4)");
            entity.Property(e => e.BktPosterior).HasColumnType("numeric(5,4)");

            entity.HasIndex(e => e.SessionId)
                  .HasDatabaseName("IX_practice_session_item_session_id");
            entity.HasIndex(e => new { e.SessionId, e.PresentedAt })
                  .HasDatabaseName("IX_practice_session_item_session_presented_at");
            entity.HasIndex(e => e.QuestionId)
                  .HasDatabaseName("IX_practice_session_item_question_id");
            entity.HasIndex(e => new { e.SessionId, e.QuestionId, e.AttemptNumber })
                  .IsUnique()
                  .HasDatabaseName("UX_practice_session_item_session_question_attempt");

            entity.HasOne(e => e.Session)
                  .WithMany(s => s.Items)
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MasteryState>(entity =>
        {
            entity.ToTable("mastery_state");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PL).HasColumnType("numeric(5,4)");
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.TopicId, e.SubtopicId })
                  .IsUnique()
                  .HasDatabaseName("UX_mastery_state_user_topic_subtopic");
            entity.HasIndex(e => new { e.UserId, e.UpdatedAt })
                  .HasDatabaseName("IX_mastery_state_user_updated_at");
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("Outbox");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.HasIndex(e => new { e.ProcessedUtc, e.OccurredUtc });
        });

        builder.Entity<UserXpEvent>(entity =>
        {
            entity.ToTable("user_xp_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SourceId).HasMaxLength(128);
            entity.Property(e => e.ValidationStatus).IsRequired().HasMaxLength(32);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.AwardedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.UserId, e.AwardedAtUtc })
                  .HasDatabaseName("IX_user_xp_events_user_awarded_at");
            entity.HasIndex(e => new { e.SchoolId, e.AwardedAtUtc })
                  .HasDatabaseName("IX_user_xp_events_school_awarded_at");
            entity.HasIndex(e => new { e.ValidationStatus, e.AwardedAtUtc })
                  .HasDatabaseName("IX_user_xp_events_validation_awarded_at");
            entity.HasIndex(e => new { e.UserId, e.SourceType, e.SourceId })
                  .IsUnique()
                  .HasFilter("\"SourceId\" IS NOT NULL")
                  .HasDatabaseName("UX_user_xp_events_user_source");

            entity.HasOne(e => e.Season)
                  .WithMany()
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CompetitionSeason>(entity =>
        {
            entity.ToTable("competition_seasons");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StartDateUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.EndDateUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.IsActive)
                  .HasDatabaseName("IX_competition_seasons_active");
        });

        builder.Entity<XpCheatLog>(entity =>
        {
            entity.ToTable("xp_cheat_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SourceId).HasMaxLength(128);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.DetectedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.UserId, e.DetectedAtUtc })
                  .HasDatabaseName("IX_xp_cheat_log_user_detected");
            entity.HasIndex(e => e.DetectedAtUtc)
                  .HasDatabaseName("IX_xp_cheat_log_detected");
        });

        builder.Entity<AnswerPatternDetectionLog>(entity =>
        {
            entity.ToTable("answer_pattern_detection_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.DeviceId).HasMaxLength(128);
            entity.Property(e => e.AnswerFingerprint).HasMaxLength(32);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Decision).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ReasonSummary).IsRequired().HasMaxLength(500);
            entity.Property(e => e.SignalsJson).HasColumnType("jsonb");
            entity.Property(e => e.PromptVersion).IsRequired().HasMaxLength(64);
            entity.Property(e => e.PromptPayloadJson).HasColumnType("jsonb");
            entity.Property(e => e.DetectionEngine).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ReviewStatus).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);
            entity.Property(e => e.MlReviewStatus).IsRequired().HasMaxLength(32);
            entity.Property(e => e.MlModelName).HasMaxLength(128);
            entity.Property(e => e.MlReviewOutputJson).HasColumnType("jsonb");
            entity.Property(e => e.MlLastError).HasMaxLength(1000);
            entity.Property(e => e.AnsweredAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.DetectedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ReviewedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.MlLastAttemptAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.MlReviewedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.UserId, e.DetectedAtUtc })
                  .HasDatabaseName("IX_answer_pattern_detection_user_detected");
            entity.HasIndex(e => new { e.ReviewStatus, e.DetectedAtUtc })
                  .HasDatabaseName("IX_answer_pattern_detection_review_detected");
            entity.HasIndex(e => new { e.Severity, e.DetectedAtUtc })
                  .HasDatabaseName("IX_answer_pattern_detection_severity_detected");
            entity.HasIndex(e => new { e.SourceType, e.AnsweredAtUtc })
                  .HasDatabaseName("IX_answer_pattern_detection_source_answered");
            entity.HasIndex(e => new { e.MlReviewStatus, e.DetectedAtUtc })
                  .HasDatabaseName("IX_answer_pattern_detection_ml_review_detected");
        });

        builder.Entity<School>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);

            entity.HasIndex(e => e.Name)
                  .HasDatabaseName("IX_Schools_Name");
        });

        builder.Entity<SchoolScoreAggregate>(entity =>
        {
            entity.ToTable("school_scores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(32);
            entity.Property(e => e.PeriodStartUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.AverageXpPerActiveStudent).HasColumnType("numeric(18,4)");
            entity.Property(e => e.ParticipationRate).HasColumnType("numeric(8,6)");
            entity.Property(e => e.CompositeScore).HasColumnType("numeric(18,6)");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.SchoolId, e.Period, e.PeriodStartUtc })
                  .IsUnique()
                  .HasDatabaseName("UX_school_scores_school_period_start");
            entity.HasIndex(e => new { e.Period, e.PeriodStartUtc, e.Rank })
                  .HasDatabaseName("IX_school_scores_period_start_rank");
            entity.HasIndex(e => new { e.Period, e.PeriodStartUtc, e.CompositeScore, e.SchoolId })
                  .HasDatabaseName("IX_school_scores_period_start_score");

            entity.Property(e => e.WeightedXp).HasColumnType("double precision").HasDefaultValue(0.0);

            entity.HasOne(e => e.Season)
                  .WithMany()
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.School)
                  .WithMany()
                  .HasForeignKey(e => e.SchoolId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SchoolRankHistory>(entity =>
        {
            entity.ToTable("school_rank_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(32);
            entity.Property(e => e.PeriodStartUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ParticipationRate).HasColumnType("numeric(8,6)");
            entity.Property(e => e.CompositeScore).HasColumnType("numeric(18,6)");
            entity.Property(e => e.SnapshotTimeUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.SchoolId, e.Period, e.PeriodStartUtc, e.SnapshotTimeUtc })
                  .HasDatabaseName("IX_school_rank_history_school_period_snapshot");
            entity.HasIndex(e => new { e.Period, e.PeriodStartUtc, e.SnapshotTimeUtc, e.Rank })
                  .HasDatabaseName("IX_school_rank_history_period_snapshot_rank");

            entity.Property(e => e.WeightedXp).HasColumnType("double precision").HasDefaultValue(0.0);

            entity.HasOne(e => e.Season)
                  .WithMany()
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.School)
                  .WithMany()
                  .HasForeignKey(e => e.SchoolId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Faculty>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.University).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);

            entity.HasIndex(e => e.Name)
                  .HasDatabaseName("IX_Faculties_Name");
        });

        // ─── 🎨 Cosmetic System ───

        builder.Entity<CosmeticSeason>(entity =>
        {
            entity.ToTable("cosmetic_seasons");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Theme).HasMaxLength(128);
            entity.Property(e => e.ThemeAssetPath).HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.StartDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.EndDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.RewardLockAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ArchiveAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.Key)
                  .IsUnique()
                  .HasDatabaseName("UX_cosmetic_seasons_key");

            entity.HasIndex(e => e.IsActive)
                  .HasDatabaseName("IX_cosmetic_seasons_active");
            entity.HasIndex(e => new { e.StartDate, e.EndDate })
                  .HasDatabaseName("IX_cosmetic_seasons_dates");
        });

        builder.Entity<CosmeticItem>(entity =>
        {
            entity.ToTable("cosmetic_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Rarity).IsRequired().HasMaxLength(20).HasDefaultValue("common");
            entity.Property(e => e.AssetPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PreviewAssetPath).HasMaxLength(500);
            entity.Property(e => e.UnlockType).IsRequired().HasMaxLength(50).HasDefaultValue("default");
            entity.Property(e => e.UnlockCondition).HasMaxLength(500);
            entity.Property(e => e.UnlockConditionJson).HasColumnType("jsonb");
            entity.Property(e => e.CompatibilityRulesJson).HasColumnType("jsonb");
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.AssetVersion).IsRequired().HasMaxLength(32).HasDefaultValue("1");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.ReleaseDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.RetirementDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Season)
                  .WithMany(s => s.Items)
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Key)
                  .IsUnique()
                  .HasDatabaseName("UX_cosmetic_items_key");

            entity.HasIndex(e => e.Category)
                  .HasDatabaseName("IX_cosmetic_items_category");
            entity.HasIndex(e => e.Rarity)
                  .HasDatabaseName("IX_cosmetic_items_rarity");
            entity.HasIndex(e => new { e.Category, e.Rarity })
                  .HasDatabaseName("IX_cosmetic_items_category_rarity");
            entity.HasIndex(e => e.SeasonId)
                  .HasDatabaseName("IX_cosmetic_items_season");
            entity.HasIndex(e => e.IsDefault)
                  .HasDatabaseName("IX_cosmetic_items_default");
            entity.HasIndex(e => new { e.IsActive, e.ReleaseDate })
                  .HasDatabaseName("IX_cosmetic_items_active_release");
        });

        builder.Entity<UserCosmeticInventory>(entity =>
        {
            entity.ToTable("user_cosmetic_inventory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SourceRef).HasMaxLength(128);
            entity.Property(e => e.GrantReason).HasMaxLength(256);
            entity.Property(e => e.AssetVersion).IsRequired().HasMaxLength(32).HasDefaultValue("1");
            entity.Property(e => e.UnlockedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.RevokedAt).HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.CosmeticItem)
                  .WithMany()
                  .HasForeignKey(e => e.CosmeticItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Each user can own an item only once
            entity.HasIndex(e => new { e.UserId, e.CosmeticItemId })
                  .IsUnique()
                  .HasDatabaseName("UX_user_cosmetic_inventory_user_item");
            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_user_cosmetic_inventory_user");
            entity.HasIndex(e => new { e.UserId, e.Source })
                  .HasDatabaseName("IX_user_cosmetic_inventory_user_source");
            entity.HasIndex(e => new { e.Source, e.SourceRef })
                  .HasDatabaseName("IX_user_cosmetic_inventory_source_ref");
        });

        builder.Entity<UserAvatarConfig>(entity =>
        {
            entity.ToTable("user_avatar_configs");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasOne<UserProfile>()
                  .WithOne()
                  .HasForeignKey<UserAvatarConfig>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Skin).WithMany().HasForeignKey(e => e.SkinId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Hair).WithMany().HasForeignKey(e => e.HairId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Clothing).WithMany().HasForeignKey(e => e.ClothingId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Accessory).WithMany().HasForeignKey(e => e.AccessoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Emoji).WithMany().HasForeignKey(e => e.EmojiId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Frame).WithMany().HasForeignKey(e => e.FrameId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Background).WithMany().HasForeignKey(e => e.BackgroundId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Effect).WithMany().HasForeignKey(e => e.EffectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.LeaderboardDecoration).WithMany().HasForeignKey(e => e.LeaderboardDecorationId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CosmeticRewardRule>(entity =>
        {
            entity.ToTable("cosmetic_reward_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ConditionJson).HasColumnType("jsonb");
            entity.Property(e => e.RewardType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.RewardPayloadJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("UX_cosmetic_reward_rules_key");
            entity.HasIndex(e => new { e.SourceType, e.IsActive, e.Priority })
                .HasDatabaseName("IX_cosmetic_reward_rules_source_active_priority");
        });

        builder.Entity<CosmeticRewardClaim>(entity =>
        {
            entity.ToTable("cosmetic_reward_claims");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.RewardKey).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SourceRef).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ClaimedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.CosmeticItem)
                .WithMany()
                .HasForeignKey(e => e.CosmeticItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.RewardKey, e.SourceRef })
                .IsUnique()
                .HasDatabaseName("UX_cosmetic_reward_claims_user_reward_source");
            entity.HasIndex(e => new { e.UserId, e.ClaimedAtUtc })
                .HasDatabaseName("IX_cosmetic_reward_claims_user_claimed_at");
        });

        builder.Entity<SeasonRewardTrackEntry>(entity =>
        {
            entity.ToTable("season_reward_tracks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TrackType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.RewardType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.RewardPayloadJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Season)
                .WithMany(x => x.RewardTrackEntries)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SeasonId, e.TrackType, e.Tier })
                .IsUnique()
                .HasDatabaseName("UX_season_reward_tracks_season_track_tier");
        });

        builder.Entity<UserAppearanceProjection>(entity =>
        {
            entity.ToTable("user_appearance_projection");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });

        builder.Entity<CosmeticTelemetryEvent>(entity =>
        {
            entity.ToTable("cosmetic_telemetry_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.OccurredAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.EventType, e.OccurredAtUtc })
                .HasDatabaseName("IX_cosmetic_telemetry_events_type_occurred");
            entity.HasIndex(e => new { e.UserId, e.OccurredAtUtc })
                .HasDatabaseName("IX_cosmetic_telemetry_events_user_occurred");
        });

        builder.Entity<CosmeticAuditLog>(entity =>
        {
            entity.ToTable("cosmetic_audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ActorUserId).HasMaxLength(450);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.BeforeJson).HasColumnType("jsonb");
            entity.Property(e => e.AfterJson).HasColumnType("jsonb");
            entity.Property(e => e.OccurredAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAtUtc })
                .HasDatabaseName("IX_cosmetic_audit_log_entity_occurred");
        });

        builder.Entity<LeaderboardSnapshot>(entity =>
        {
            entity.ToTable("leaderboard_snapshot");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Scope).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(32);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.Scope, e.Period, e.Rank })
                .HasDatabaseName("IX_leaderboard_snapshot_scope_period_rank");
            entity.HasIndex(e => new { e.Scope, e.Period, e.UserId })
                .IsUnique()
                .HasDatabaseName("UX_leaderboard_snapshot_scope_period_user");
        });

        builder.Entity<UserQuizSummary>(entity =>
        {
            entity.ToTable("user_quiz_summary");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.UpdatedAtUtc)
                .HasDatabaseName("IX_user_quiz_summary_updated_at");
        });

        builder.Entity<UserRewardState>(entity =>
        {
            entity.ToTable("user_reward_state");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.RewardKey).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ClaimedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.UserId, e.RewardKey })
                .IsUnique()
                .HasDatabaseName("UX_user_reward_state_user_reward");
            entity.HasIndex(e => new { e.UserId, e.Eligible, e.Claimed })
                .HasDatabaseName("IX_user_reward_state_user_status");
        });
    }
}
