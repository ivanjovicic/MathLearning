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
        
        Console.WriteLine("=== Checking Database Indexes ===\n");
        
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
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"{"Table",-20} {"Index Name",-40}");
            Console.WriteLine(new string('-', 80));
            
            int count = 0;
            var foundIndexes = new List<string>();
            
            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(1);
                var indexName = reader.GetString(2);
                
                Console.WriteLine($"{tableName,-20} {indexName,-40}");
                foundIndexes.Add(indexName);
                count++;
            }
            
            Console.WriteLine(new string('-', 80));
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
            foreach (var idx in expectedIndexes)
            {
                var status = foundIndexes.Contains(idx) ? "✓" : "✗";
                Console.WriteLine($"  {status} {idx}");
            }
            
            if (count == expectedIndexes.Length && expectedIndexes.All(x => foundIndexes.Contains(x)))
            {
                Console.WriteLine("\n✓ All expected indexes are created!");
            }
            else if (count < expectedIndexes.Length)
            {
                Console.WriteLine($"\n⚠ Warning: Only {count} out of {expectedIndexes.Length} indexes found!");
                var missing = expectedIndexes.Except(foundIndexes).ToList();
                if (missing.Any())
                {
                    Console.WriteLine("\nMissing indexes:");
                    foreach (var idx in missing)
                    {
                        Console.WriteLine($"  - {idx}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"\n✓ All indexes present (found {count} indexes)");
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
