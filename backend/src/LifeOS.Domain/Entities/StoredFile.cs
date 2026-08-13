using LifeOS.Domain.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Метаданные файла, физически хранящегося в Firebase Storage.
/// Класс назван StoredFile, чтобы не конфликтовать с System.IO.File.
/// В БД таблица называется "Files".
/// </summary>
public class StoredFile : BaseEntity
{
    public Guid UserId { get; set; }

    public string FileName { get; set; } = null!;

    /// <summary>Публичный/подписанный URL объекта в Firebase Storage.</summary>
    public string FirebaseUrl { get; set; } = null!;

    /// <summary>Путь объекта внутри bucket — нужен для удаления файла.</summary>
    public string StoragePath { get; set; } = null!;

    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }

    public ModuleType Module { get; set; } = ModuleType.General;

    public User User { get; set; } = null!;
}
