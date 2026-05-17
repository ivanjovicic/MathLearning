namespace MathLearning.Tests.Startup;

public sealed class CorsConfigurationGuardTests
{
    [Fact]
    public void ServiceRegistrationExtensions_KeepsEnvironmentAwareCorsMarkers()
    {
        var source = ReadServiceRegistrationExtensions();

        Assert.Contains("IsDevelopment", source);
        Assert.Contains("IsEnvironment(\"Test\")", source);
        Assert.Contains("Cors:AllowedOrigins", source);
        Assert.Contains("WithOrigins", source);
    }

    [Fact]
    public void AddCorsAndSwagger_IsNotOnlyAllowAnyOriginDefaultPolicy()
    {
        var source = ReadServiceRegistrationExtensions();
        var methodBody = ExtractMethodBody(source, "public static void AddCorsAndSwagger");
        var allowAnyOriginIndex = methodBody.IndexOf("AllowAnyOrigin", StringComparison.Ordinal);
        var withOriginsIndex = methodBody.IndexOf("WithOrigins", StringComparison.Ordinal);

        Assert.True(allowAnyOriginIndex >= 0, "Expected development/test CORS branch to allow any origin.");
        Assert.True(withOriginsIndex > allowAnyOriginIndex, "Expected non-dev CORS branch to use configured WithOrigins after the permissive branch.");
    }

    private static string ReadServiceRegistrationExtensions()
    {
        var path = FindServiceRegistrationExtensionsPath();
        return File.ReadAllText(path);
    }

    private static string FindServiceRegistrationExtensionsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MathLearning.Api", "Startup", "ServiceRegistrationExtensions.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find method signature '{signature}'.");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Could not find method body for '{signature}'.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[bodyStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not extract method body for '{signature}'.");
    }
}
