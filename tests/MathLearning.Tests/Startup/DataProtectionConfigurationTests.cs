using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MathLearning.Api.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Startup;

public sealed class DataProtectionConfigurationTests
{
    [Fact]
    public void ProductionRequiresDurableKeyPathAndCertificate()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => DataProtectionConfiguration.Resolve(builder.Configuration, builder.Environment));

        Assert.Contains("DataProtection:KeysPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentDoesNotRequireProductionKeyMaterial()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        Assert.Null(DataProtectionConfiguration.Resolve(builder.Configuration, builder.Environment));
    }

    [Fact]
    public void ProductionConfiguresPersistentAndEncryptedDataProtectionKeys()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"mathlearning-dp-{Guid.NewGuid():N}");
        const string certificatePassword = "test-only-certificate-password";

        using var sourceCertificate = CreateTestCertificate();
        var certificateBase64 = Convert.ToBase64String(
            sourceCertificate.Export(X509ContentType.Pfx, certificatePassword));

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Production"
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DataProtectionConfiguration.KeysPathKey] = keyPath,
                [DataProtectionConfiguration.CertificateBase64Key] = certificateBase64,
                [DataProtectionConfiguration.CertificatePasswordKey] = certificatePassword
            });

            builder.AddDataProtectionServices();

            using var provider = builder.Services.BuildServiceProvider();
            var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
            var protector = dataProtection.CreateProtector("configuration-test");
            var protectedValue = protector.Protect("survives-restart");

            Assert.Equal("survives-restart", protector.Unprotect(protectedValue));
            Assert.NotEmpty(Directory.EnumerateFiles(keyPath));
        }
        finally
        {
            if (Directory.Exists(keyPath))
            {
                Directory.Delete(keyPath, recursive: true);
            }
        }
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=MathLearning.Tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));

        return new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }
}
