using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathLearning.Application.DTOs.Sync;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncRequestValidationException : Exception
{
    public SyncRequestValidationException(int statusCode, string errorCode, string publicMessage)
        : base(publicMessage)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        PublicMessage = publicMessage;
    }

    public int StatusCode { get; }
    public string ErrorCode { get; }
    public string PublicMessage { get; }
}

internal static class SyncRequestValidation
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex SensitiveValueRegex = new(
        "(?i)(password|pwd|token|secret|apikey)\\s*=\\s*[^;\\s]+",
        RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedOperationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "submit_answer"
    };

    public static void ValidateRegisterDeviceRequest(RegisterSyncDeviceRequest request, SyncOptions options)
    {
        ValidateRequiredText(request.DeviceId, options.MaxDeviceIdLength, "device_id_required", "DeviceId is required.");
        ValidateRequiredText(request.Platform, options.MaxPlatformLength, "platform_required", "Platform is required.");
        ValidateOptionalText(request.DeviceName, options.MaxDeviceNameLength, "device_name_too_long", "DeviceName exceeds the allowed length.");
        ValidateOptionalText(request.AppVersion, options.MaxAppVersionLength, "app_version_too_long", "AppVersion exceeds the allowed length.");
    }

    public static int ValidateSyncRequestEnvelope(string authenticatedUserId, SyncRequestDto request, SyncOptions options)
    {
        ValidateRequiredText(request.DeviceId, options.MaxDeviceIdLength, "device_id_required", "DeviceId is required.");

        if (request.Operations is null)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "operations_required",
                "Operations are required.");
        }

        var maxBatchSize = options.MaxOperationsPerBatch > 0 ? options.MaxOperationsPerBatch : options.MaxBatchSize;
        if (request.Operations.Count > maxBatchSize)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status422UnprocessableEntity,
                "batch_too_large",
                $"Batch too large. MaxOperationsPerBatch={maxBatchSize}.");
        }

        long totalPayloadBytes = 0;
        foreach (var operation in request.Operations)
        {
            ValidateOperationEnvelope(authenticatedUserId, request.DeviceId, operation, options);

            var payloadJson = operation.Payload.GetRawText();
            var payloadBytes = Encoding.UTF8.GetByteCount(payloadJson);
            if (payloadBytes > options.MaxOperationPayloadBytes)
            {
                throw new SyncRequestValidationException(
                    StatusCodes.Status422UnprocessableEntity,
                    "payload_too_large",
                    $"Operation payload exceeded the allowed size. MaxOperationPayloadBytes={options.MaxOperationPayloadBytes}.");
            }

            totalPayloadBytes += payloadBytes;
            if (totalPayloadBytes > options.MaxTotalPayloadBytes)
            {
                throw new SyncRequestValidationException(
                    StatusCodes.Status413PayloadTooLarge,
                    "payload_too_large",
                    $"Total payload bytes exceeded the allowed limit. MaxTotalPayloadBytes={options.MaxTotalPayloadBytes}.");
            }

            ValidateOperationPayloadSchema(operation.OperationType, operation.Payload, options);
        }

        if (totalPayloadBytes > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)totalPayloadBytes;
    }

    public static string BuildSafeFailureReason(string category, string publicMessage, int maxLength)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var correlationId = Activity.Current?.Id;
        var text = new StringBuilder()
            .Append(category)
            .Append(": ")
            .Append(publicMessage);

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            text.Append(" traceId=").Append(traceId);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            text.Append(" correlationId=").Append(correlationId);
        }

        return NormalizePersistedText(text.ToString(), maxLength);
    }

    public static string BuildBoundedPublicMessage(string message, int maxLength) =>
        NormalizePersistedText(message, maxLength);

    private static void ValidateOperationEnvelope(
        string authenticatedUserId,
        string requestDeviceId,
        SyncOperationDto operation,
        SyncOptions options)
    {
        if (operation.OperationId == Guid.Empty)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "invalid_operation_id",
                "OperationId is required.");
        }

        if (!string.Equals(operation.UserId, authenticatedUserId, StringComparison.Ordinal))
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "user_mismatch",
                "Operation user does not match authenticated user.");
        }

        if (!string.Equals(operation.DeviceId, requestDeviceId, StringComparison.Ordinal))
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "device_mismatch",
                "Operation device does not match request device.");
        }

        if (operation.ClientSequence <= 0)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "invalid_sequence",
                "ClientSequence must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(operation.OperationType))
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status422UnprocessableEntity,
                "operation_type_required",
                "OperationType is required.");
        }

        var operationType = operation.OperationType.Trim();
        if (Encoding.UTF8.GetByteCount(operationType) > options.MaxOperationTypeLength)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "operation_type_too_long",
                $"OperationType exceeds the allowed length. MaxOperationTypeLength={options.MaxOperationTypeLength}.");
        }

        if (!AllowedOperationTypes.Contains(operationType))
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status422UnprocessableEntity,
                "unsupported_operation",
                $"Unsupported operation type '{operationType}'.");
        }

        if (operation.OccurredAtUtc == default)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                "invalid_timestamp",
                "OccurredAtUtc is required.");
        }

        if (options.RequireOperationSignatures)
        {
            if (string.IsNullOrWhiteSpace(operation.Signature))
            {
                throw new SyncRequestValidationException(
                    StatusCodes.Status400BadRequest,
                    "missing_signature",
                    "Operation signature is required.");
            }

            var signature = operation.Signature.Trim();
            if (Encoding.UTF8.GetByteCount(signature) > options.MaxSignatureBytes)
            {
                throw new SyncRequestValidationException(
                    StatusCodes.Status400BadRequest,
                    "signature_too_long",
                    $"Operation signature exceeds the allowed length. MaxSignatureBytes={options.MaxSignatureBytes}.");
            }
        }
    }

    private static void ValidateOperationPayloadSchema(
        string operationType,
        JsonElement payload,
        SyncOptions options)
    {
        var normalizedType = operationType.Trim().ToLowerInvariant();
        switch (normalizedType)
        {
            case "submit_answer":
                if (payload.ValueKind != JsonValueKind.Object)
                {
                    throw new SyncRequestValidationException(
                        StatusCodes.Status422UnprocessableEntity,
                        "invalid_payload",
                        "Submit answer payload is invalid.");
                }

                SubmitAnswerSyncPayloadDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<SubmitAnswerSyncPayloadDto>(payload.GetRawText(), SerializerOptions);
                }
                catch (JsonException)
                {
                    throw new SyncRequestValidationException(
                        StatusCodes.Status422UnprocessableEntity,
                        "invalid_payload",
                        "Submit answer payload is invalid.");
                }

                if (dto is null ||
                    dto.QuestionId <= 0 ||
                    string.IsNullOrWhiteSpace(dto.SessionId) ||
                    string.IsNullOrWhiteSpace(dto.Answer) ||
                    dto.AnsweredAtUtc == default)
                {
                    throw new SyncRequestValidationException(
                        StatusCodes.Status422UnprocessableEntity,
                        "invalid_payload",
                        "Submit answer payload is incomplete.");
                }

                if (dto.TimeSpentSeconds < 0)
                {
                    throw new SyncRequestValidationException(
                        StatusCodes.Status422UnprocessableEntity,
                        "invalid_payload",
                        "Submit answer payload is incomplete.");
                }

                return;

            default:
                throw new SyncRequestValidationException(
                    StatusCodes.Status422UnprocessableEntity,
                    "unsupported_operation",
                    $"Unsupported operation type '{operationType}'.");
        }
    }

    private static void ValidateRequiredText(string? value, int maxLength, string errorCode, string publicMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new SyncRequestValidationException(StatusCodes.Status400BadRequest, errorCode, publicMessage);
        }

        if (Encoding.UTF8.GetByteCount(trimmed) > maxLength)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                errorCode,
                $"{publicMessage} MaxLength={maxLength}.");
        }
    }

    private static void ValidateOptionalText(string? value, int maxLength, string errorCode, string publicMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (Encoding.UTF8.GetByteCount(trimmed) > maxLength)
        {
            throw new SyncRequestValidationException(
                StatusCodes.Status400BadRequest,
                errorCode,
                $"{publicMessage} MaxLength={maxLength}.");
        }
    }

    private static string NormalizePersistedText(string value, int maxLength)
    {
        var normalized = SensitiveValueRegex.Replace(value, "$1=<redacted>")
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        if (maxLength <= 3)
        {
            return normalized[..maxLength];
        }

        return $"{normalized[..(maxLength - 3)]}...";
    }
}
