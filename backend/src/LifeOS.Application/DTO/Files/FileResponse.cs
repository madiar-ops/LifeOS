using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Files;

public record FileResponse(
    Guid Id,
    string FileName,
    string Url,
    string ContentType,
    long SizeBytes,
    ModuleType Module,
    DateTime CreatedAt);
