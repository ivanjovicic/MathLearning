using System.Net;
using System.Net.Mail;
using Hangfire;
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

public sealed class PasswordResetDeliveryOptionsValidator : IValidateOptions<PasswordResetDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, PasswordResetDeliveryOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.SmtpHost))
            failures.Add("SmtpHost is required when password reset delivery is enabled.");
        if (options.SmtpPort is < 1 or > 65535)
            failures.Add("SmtpPort must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(options.FromAddress) ||
            !TryParseExactAddress(options.FromAddress, out _))
            failures.Add("FromAddress must be a valid email address without a display-name wrapper.");
        if (!Uri.TryCreate(options.ResetBaseUrl, UriKind.Absolute, out var resetUri) ||
            string.IsNullOrWhiteSpace(resetUri.Scheme) ||
            string.Equals(resetUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            failures.Add("ResetBaseUrl must be a valid application or deep-link URI.");

        var hasUsername = !string.IsNullOrWhiteSpace(options.SmtpUsername);
        var hasPassword = !string.IsNullOrWhiteSpace(options.SmtpPassword);
        if (hasUsername != hasPassword)
            failures.Add("SmtpUsername and SmtpPassword must be configured together.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool TryParseExactAddress(string value, out MailAddress? address)
    {
        address = null;
        try
        {
            address = new MailAddress(value.Trim());
            return string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record PasswordResetDeliveryMessage(
    string RecipientEmail,
    string ResetToken,
    string ResetUrl);

public interface IPasswordResetDelivery
{
    Task<bool> SendAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default);
}

public interface IPasswordResetDeliveryDispatcher
{
    Task DispatchAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default);
}

public interface IPasswordResetDeliveryJob
{
    Task SendAsync(PasswordResetDeliveryMessage message);
}

public sealed class PasswordResetDeliveryJob : IPasswordResetDeliveryJob
{
    private readonly IPasswordResetDelivery delivery;
    private readonly ILogger<PasswordResetDeliveryJob> logger;

    public PasswordResetDeliveryJob(
        IPasswordResetDelivery delivery,
        ILogger<PasswordResetDeliveryJob> logger)
    {
        this.delivery = delivery;
        this.logger = logger;
    }

    public async Task SendAsync(PasswordResetDeliveryMessage message)
    {
        try
        {
            if (!await delivery.SendAsync(message, CancellationToken.None))
            {
                logger.LogWarning(
                    "Password-reset delivery unavailable. Reason={Reason}",
                    "delivery_unavailable");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Password-reset delivery failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            throw;
        }
    }
}

public sealed class InlinePasswordResetDeliveryDispatcher : IPasswordResetDeliveryDispatcher
{
    private readonly IPasswordResetDelivery delivery;

    public InlinePasswordResetDeliveryDispatcher(IPasswordResetDelivery delivery)
    {
        this.delivery = delivery;
    }

    public async Task DispatchAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default)
    {
        await delivery.SendAsync(message, cancellationToken);
    }
}

public sealed class HangfirePasswordResetDeliveryDispatcher : IPasswordResetDeliveryDispatcher
{
    private readonly IBackgroundJobClient backgroundJobs;

    public HangfirePasswordResetDeliveryDispatcher(IBackgroundJobClient backgroundJobs)
    {
        this.backgroundJobs = backgroundJobs;
    }

    public Task DispatchAsync(PasswordResetDeliveryMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        backgroundJobs.Enqueue<IPasswordResetDeliveryJob>(job => job.SendAsync(message));
        return Task.CompletedTask;
    }
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
