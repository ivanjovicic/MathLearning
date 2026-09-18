using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MathLearning.Api.Services;

public sealed class PasswordResetDeliveryOptions
{
    public const string SectionName = "PasswordReset:Delivery";

    public bool Enabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string? FromAddress { get; set; }
    public string? FromDisplayName { get; set; } = "MathLearning";
    public string ResetBaseUrl { get; set; } = "mathlearning://reset-password";
}

public sealed record PasswordResetDeliveryMessage(
    string RecipientEmail,
    string ResetToken,
    string ResetUrl);

public interface IPasswordResetDelivery
{
    Task<bool> SendAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicitly non-sending fallback. Production remains fail-closed unless a real provider is configured.
/// </summary>
public sealed class UnavailablePasswordResetDelivery : IPasswordResetDelivery
{
    public Task<bool> SendAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

/// <summary>Deterministic provider for Development/Test; it stores no secrets in logs.</summary>
public sealed class InMemoryPasswordResetDelivery : IPasswordResetDelivery
{
    private readonly List<PasswordResetDeliveryMessage> messages = new();
    private readonly object gate = new();

    public IReadOnlyList<PasswordResetDeliveryMessage> Messages
    {
        get
        {
            lock (gate)
                return messages.ToArray();
        }
    }

    public Task<bool> SendAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default)
    {
        lock (gate)
            messages.Add(message);
        return Task.FromResult(true);
    }
}

public sealed class SmtpPasswordResetDelivery : IPasswordResetDelivery
{
    private readonly PasswordResetDeliveryOptions options;

    public SmtpPasswordResetDelivery(IOptions<PasswordResetDeliveryOptions> options)
    {
        this.options = options.Value;
    }

    public async Task<bool> SendAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(options.FromAddress!, options.FromDisplayName),
            Subject = "Reset your MathLearning password",
            Body = $"Use this link to reset your MathLearning password: {message.ResetUrl}",
            IsBodyHtml = false
        };
        mail.To.Add(new MailAddress(message.RecipientEmail));

        using var client = new SmtpClient(options.SmtpHost!, options.SmtpPort)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
            client.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword);

        await client.SendMailAsync(mail, cancellationToken);
        return true;
    }
}
