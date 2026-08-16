using System.Text.Json;
using LifeOS.Application.Common;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeOS.Application.Services;

/// <inheritdoc />
public class AiHistoryRecorder : IAiHistoryRecorder
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly AiSettings _settings;
    private readonly ILogger<AiHistoryRecorder> _logger;

    /// <summary>Ограничение размера payload: полный текст PDF в jsonb раздул бы таблицу.</summary>
    private const int MaxPayloadLength = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AiHistoryRecorder(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IOptions<AiSettings> settings,
        ILogger<AiHistoryRecorder> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RecordAsync(
        string endpoint,
        object request,
        object response,
        decimal? confidence,
        bool isConfident,
        ModuleType module,
        string? recommendationText = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var entry = new AiHistoryEntry
        {
            UserId = userId,
            Endpoint = endpoint,
            RequestPayload = Serialize(request),
            ResponsePayload = Serialize(response),
            Confidence = confidence
        };

        await _unitOfWork.AiHistory.AddAsync(entry, cancellationToken);

        // Рекомендация создаётся ТОЛЬКО при достаточной уверенности.
        // Иначе лента засорялась бы догадками модели, и пользователь
        // перестал бы ей доверять — прямое следствие принципа
        // «AI не генерирует случайные ответы».
        if (isConfident
            && confidence >= _settings.RecommendationThreshold
            && !string.IsNullOrWhiteSpace(recommendationText))
        {
            await _unitOfWork.Recommendations.AddAsync(
                new Recommendation
                {
                    UserId = userId,
                    Module = module,
                    Content = recommendationText.Trim(),
                    Confidence = confidence.Value
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AI-вызов {Endpoint} пользователя {UserId}: уверенность {Confidence}",
            endpoint, userId, confidence);
    }

    private static string Serialize(object value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);

            if (json.Length <= MaxPayloadLength)
                return json;

            // Обрезанный JSON перестал бы быть валидным jsonb, поэтому
            // сохраняем пометку об усечении как корректный объект.
            return JsonSerializer.Serialize(
                new { truncated = true, length = json.Length, preview = json[..MaxPayloadLength] },
                JsonOptions);
        }
        catch (NotSupportedException)
        {
            // Аудит не должен ронять основную операцию.
            return "{\"error\":\"serialization_failed\"}";
        }
    }
}
