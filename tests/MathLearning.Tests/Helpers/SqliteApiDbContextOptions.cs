using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MathLearning.Tests.Helpers;

internal static class SqliteApiDbContextOptions
{
    public static DbContextOptions<ApiDbContext> Create(SqliteConnection connection)
        => new DbContextOptionsBuilder<ApiDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>()
            .Options;

    private sealed class SqliteCompatibleModelCustomizer(
        ModelCustomizerDependencies dependencies)
        : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            modelBuilder.Entity<QuestionStat>()
                .Property(x => x.NextReview)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Question>().Ignore("xmin");
        }
    }
}
