using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MathLearning.Api;

namespace MathLearning.Tests.Services;

public sealed class ProgramFatalStartupTests
{
    [Fact]
    public async Task DatabaseStartupFailure_LogsFatalAndReturnsNonZeroExitCode()
    {
        var apiAssembly = typeof(Program).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var dataProtectionKeysPath = Path.Combine(Path.GetTempPath(), $"mathlearning-startup-probe-{Guid.NewGuid():N}");
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            "CN=MathLearning startup probe",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));
        const string certificatePassword = "startup-probe-only";

        startInfo.ArgumentList.Add(apiAssembly);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        startInfo.Environment["Database__StartupMode"] = "ValidateExact";
        startInfo.Environment["ConnectionStrings__Default"] =
            "Host=127.0.0.1;Port=1;Database=startup_probe;Username=probe;Password=test-only;Timeout=1;Command Timeout=1";
        startInfo.Environment["JwtSettings__SecretKey"] = "integration-test-only-key-with-more-than-32-chars";
        startInfo.Environment["DataProtection__KeysPath"] = dataProtectionKeysPath;
        startInfo.Environment["DataProtection__CertificateBase64"] =
            Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, certificatePassword));
        startInfo.Environment["DataProtection__CertificatePassword"] = certificatePassword;
        startInfo.Environment["Redis__Required"] = "false";
        startInfo.Environment.Remove("ConnectionStrings__Redis");
        startInfo.Environment.Remove("Redis__ConnectionString");

        try
        {
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("The startup-failure probe did not terminate within 30 seconds.");
            }

            var logs = await Task.WhenAll(stdoutTask, stderrTask);
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Database startup failed.", string.Concat(logs), StringComparison.Ordinal);
            Assert.Contains("Application terminated unexpectedly", string.Concat(logs), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dataProtectionKeysPath))
                Directory.Delete(dataProtectionKeysPath, recursive: true);
        }
    }
}
