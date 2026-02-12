using Npgsql;
using System;

class Program
{
    static async Task Main(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("✗ Missing connection string. Set CONNECTION_STRING (or ConnectionStrings__Default) environment variable.");
            Environment.Exit(2);
            return;
        }
        
        var checkIndexesQuery = @"
            SELECT 
                schemaname,
                tablename,
                indexname,
                indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND (
                indexname LIKE 'IX_Questions_%' OR
                indexname LIKE 'IX_Subtopics_%' OR
                indexname LIKE 'IX_UserAnswers_%' OR
                indexname LIKE 'IX_UserQuestionStats_%'
              )
            ORDER BY tablename, indexname;";
        
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("✓ Connected to database\n");
            
            await using var command = new NpgsqlCommand(checkIndexesQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            
            Console.WriteLine("Indexes in database:");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"{"Table",-20} {"Index Name",-40} {"Definition"}");
            Console.WriteLine(new string('-', 100));
            
            int count = 0;
            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(1);
                var indexName = reader.GetString(2);
                var indexDef = reader.GetString(3);
                
                Console.WriteLine($"{tableName,-20} {indexName,-40}");
                count++;
            }
            
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"\nTotal indexes found: {count}");
            
            // Expected indexes from migration
            var expectedIndexes = new[]
            {
                "IX_Questions_SubtopicId",
                "IX_Subtopics_TopicId",
                "IX_UserAnswers_QuestionId",
                "IX_UserAnswers_QuizSessionId",
                "IX_UserQuestionStats_QuestionId"
            };
            
            Console.WriteLine($"\nExpected indexes: {expectedIndexes.Length}");
            Console.WriteLine(string.Join("\n", expectedIndexes.Select(x => $"  - {x}")));
            
            if (count == expectedIndexes.Length)
            {
                Console.WriteLine("\n✓ All expected indexes are created!");
            }
            else if (count < expectedIndexes.Length)
            {
                Console.WriteLine($"\n⚠ Warning: Only {count} out of {expectedIndexes.Length} indexes found!");
            }
            else
            {
                Console.WriteLine($"\n✓ Found {count} indexes (including primary key indexes)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
            Environment.Exit(1);
        }
    }
}
