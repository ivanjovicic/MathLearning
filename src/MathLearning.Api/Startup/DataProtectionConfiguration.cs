using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Api.Startup;

public sealed record DataProtectionSettings(string KeysPath, X509Certificate2 Certificate);

public static class DataProtectionConfiguration
{
    public const string KeysPathKey = "DataProtection:KeysPath";
    public const string CertificatePathKey = "DataProtection:CertificatePath";
    public const string CertificateBase64Key = "DataProtection:CertificateBase64";
    public const string CertificatePasswordKey = "DataProtection:CertificatePassword";

    public static void AddDataProtectionServices(this WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName("MathLearning.Api");

        var settings = Resolve(builder.Configuration, builder.Environment);
        if (settings is null)
        {
            return;
        }

        dataProtection
            .PersistKeysToFileSystem(new DirectoryInfo(settings.KeysPath))
            .ProtectKeysWithCertificate(settings.Certificate);

        Serilog.Log.Information(
            "DataProtection configured with durable key storage and certificate encryption. KeysPath={KeysPath}",
            settings.KeysPath);
    }

    public static DataProtectionSettings? Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Test"))
        {
            return null;
        }

        var keysPath = configuration[KeysPathKey];
        if (string.IsNullOrWhiteSpace(keysPath) || !Path.IsPathRooted(keysPath))
        {
            throw new InvalidOperationException(
                $"{KeysPathKey} must be an absolute path on durable storage outside Development/Test.");
        }

        var certificatePath = configuration[CertificatePathKey];
        var certificateBase64 = configuration[CertificateBase64Key];
        if (!string.IsNullOrWhiteSpace(certificatePath) && !string.IsNullOrWhiteSpace(certificateBase64))
        {
            throw new InvalidOperationException(
                $"Configure only one of {CertificatePathKey} or {CertificateBase64Key}.");
        }

        var certificatePassword = configuration[CertificatePasswordKey];
        if (string.IsNullOrWhiteSpace(certificatePassword))
        {
            throw new InvalidOperationException(
                $"{CertificatePasswordKey} must be configured outside Development/Test.");
        }

        if (string.IsNullOrWhiteSpace(certificatePath) && string.IsNullOrWhiteSpace(certificateBase64))
        {
            throw new InvalidOperationException(
                $"Configure {CertificatePathKey} or {CertificateBase64Key} outside Development/Test.");
        }

        try
        {
            var certificate = LoadCertificate(certificatePath, certificateBase64, certificatePassword);
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new InvalidOperationException(
                    "The DataProtection certificate must contain a private key so keys can be decrypted after restart.");
            }

            return new DataProtectionSettings(keysPath, certificate);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "The configured DataProtection certificate could not be loaded. Check the secret-managed certificate and password.",
                ex);
        }
    }

    private static X509Certificate2 LoadCertificate(
        string? certificatePath,
        string? certificateBase64,
        string certificatePassword)
    {
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            if (!Path.IsPathRooted(certificatePath))
            {
                throw new InvalidOperationException(
                    $"{CertificatePathKey} must be an absolute path outside Development/Test.");
            }

            return new X509Certificate2(
                certificatePath,
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);
        }

        byte[] certificateBytes;
        try
        {
            certificateBytes = Convert.FromBase64String(certificateBase64!);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"{CertificateBase64Key} is not valid base64.",
                ex);
        }

        return new X509Certificate2(
            certificateBytes,
            certificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
    }
}
