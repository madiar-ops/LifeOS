using AutoMapper;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Files;
using LifeOS.Application.Files;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeOS.Application.Services;

/// <summary>
/// Работа с файлами: валидация, загрузка в хранилище, метаданные в БД.
///
/// Физический файл и запись в БД должны существовать вместе. Порядок такой:
/// сначала загрузка в хранилище, потом запись в БД; если запись упала —
/// файл удаляется, чтобы не оставлять «сирот» в bucket.
/// </summary>
public class FileService : IFileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly FileStorageSettings _settings;
    private readonly ILogger<FileService> _logger;

    public FileService(
        IUnitOfWork unitOfWork,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IMapper mapper,
        IOptions<FileStorageSettings> settings,
        ILogger<FileService> logger)
    {
        _unitOfWork = unitOfWork;
        _storage = storage;
        _currentUser = currentUser;
        _mapper = mapper;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<FileResponse>> GetAllAsync(
        FileQueryParams query, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.Files.Query().Where(f => f.UserId == userId);

        if (query.Module.HasValue)
            source = source.Where(f => f.Module == query.Module.Value);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(f => f.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StoredFile>(items, totalCount, query.PageNumber, query.PageSize)
            .Map(_mapper.Map<FileResponse>);
    }

    public async Task<FileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _mapper.Map<FileResponse>(await LoadOwnedAsync(id, tracked: false, cancellationToken));

    public async Task<FileResponse> UploadAsync(
        FileUploadData upload, ModuleType module, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        await ValidateAsync(upload, module, _settings.MaxFileSizeMb, cancellationToken);

        var stored = await SaveAsync(upload, module, userId, cancellationToken);

        return _mapper.Map<FileResponse>(stored);
    }

    /// <summary>
    /// Аватар. Старый файл удаляется — иначе хранилище копило бы
    /// все когда-либо загруженные картинки профиля.
    /// </summary>
    public async Task<string> UploadAvatarAsync(
        FileUploadData upload, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        await ValidateAsync(upload, ModuleType.Avatar, FileValidationRules.AvatarMaxSizeMb, cancellationToken);

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        var previousAvatars = await _unitOfWork.Files.Query(asNoTracking: false)
            .Where(f => f.UserId == userId && f.Module == ModuleType.Avatar)
            .ToListAsync(cancellationToken);

        var stored = await SaveAsync(upload, ModuleType.Avatar, userId, cancellationToken);

        user.AvatarUrl = stored.FirebaseUrl;

        if (previousAvatars.Count > 0)
            _unitOfWork.Files.RemoveRange(previousAvatars);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Файлы из хранилища удаляем ПОСЛЕ успешного коммита: если бы удалили
        // раньше и транзакция откатилась, профиль ссылался бы в пустоту.
        foreach (var old in previousAvatars)
            await SafeDeleteFromStorageAsync(old.StoragePath, cancellationToken);

        return stored.FirebaseUrl;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await LoadOwnedAsync(id, tracked: true, cancellationToken);

        // Файл может быть привязан к учебному материалу или резюме.
        // В БД стоит правило NoAction — удаление упало бы ошибкой внешнего ключа.
        // Проверяем заранее и отдаём понятную 409.
        await EnsureNotReferencedAsync(id, cancellationToken);

        var storagePath = file.StoragePath;

        _unitOfWork.Files.Remove(file);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await SafeDeleteFromStorageAsync(storagePath, cancellationToken);
    }

    // ---- Внутренняя логика -----------------------------------------------

    private async Task<StoredFile> SaveAsync(
        FileUploadData upload, ModuleType module, Guid userId, CancellationToken cancellationToken)
    {
        var result = await _storage.UploadAsync(
            upload.Content, upload.FileName, upload.ContentType, userId, module, cancellationToken);

        var stored = new StoredFile
        {
            UserId = userId,
            FileName = SanitizeFileName(upload.FileName),
            FirebaseUrl = result.Url,
            StoragePath = result.StoragePath,
            ContentType = upload.ContentType,
            SizeBytes = upload.Length,
            Module = module
        };

        try
        {
            await _unitOfWork.Files.AddAsync(stored, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Компенсация: физический файл уже в хранилище, а метаданные не легли.
            // Без этого в bucket копились бы файлы, о которых система не знает.
            await SafeDeleteFromStorageAsync(result.StoragePath, cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Загружен файл {FileId} ({Size} байт, {Module}) провайдером {Provider}",
            stored.Id, stored.SizeBytes, module, _storage.ProviderName);

        return stored;
    }

    private async Task ValidateAsync(
        FileUploadData upload, ModuleType module, int maxSizeMb, CancellationToken cancellationToken)
    {
        if (upload.Length <= 0)
            throw new BusinessRuleException("Файл пустой.", "file.empty");

        var maxBytes = (long)maxSizeMb * 1024 * 1024;
        if (upload.Length > maxBytes)
            throw new BusinessRuleException(
                $"Файл больше допустимых {maxSizeMb} МБ.", "file.too_large");

        var contentType = upload.ContentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;

        if (!FileValidationRules.AllowedContentTypes.TryGetValue(module, out var allowed)
            || !allowed.Contains(contentType))
        {
            throw new BusinessRuleException(
                $"Тип файла '{contentType}' не поддерживается модулем {module}. " +
                $"Допустимые: {string.Join(", ", allowed ?? Array.Empty<string>())}.",
                "file.type_not_allowed");
        }

        var extension = Path.GetExtension(upload.FileName)?.ToLowerInvariant() ?? string.Empty;

        if (FileValidationRules.ExtensionsByContentType.TryGetValue(contentType, out var extensions)
            && !extensions.Contains(extension))
        {
            throw new BusinessRuleException(
                $"Расширение '{extension}' не соответствует типу '{contentType}'.",
                "file.extension_mismatch");
        }

        await EnsureSignatureMatchesAsync(upload, contentType, cancellationToken);
    }

    /// <summary>
    /// Сверка первых байтов с сигнатурой формата.
    /// Content-Type присылает клиент, и подделать его тривиально — эта проверка
    /// не даёт залить исполняемый файл под видом PDF.
    /// </summary>
    private static async Task EnsureSignatureMatchesAsync(
        FileUploadData upload, string contentType, CancellationToken cancellationToken)
    {
        if (!FileValidationRules.Signatures.TryGetValue(contentType, out var signatures))
            return;

        if (!upload.Content.CanSeek)
            return;

        var maxLength = signatures.Max(s => s.Length);
        var header = new byte[maxLength];

        upload.Content.Position = 0;
        var read = await upload.Content.ReadAsync(header.AsMemory(0, maxLength), cancellationToken);
        upload.Content.Position = 0;

        var matches = signatures.Any(signature =>
            read >= signature.Length &&
            header.Take(signature.Length).SequenceEqual(signature));

        if (!matches)
            throw new BusinessRuleException(
                "Содержимое файла не соответствует заявленному типу.", "file.signature_mismatch");
    }

    private async Task EnsureNotReferencedAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var usedInStudy = await _unitOfWork.StudyMaterials.AnyAsync(
            m => m.FileId == fileId, cancellationToken);

        if (usedInStudy)
            throw new ConflictException(
                "Файл используется учебным материалом. Сначала удалите материал.",
                "file.in_use_study");

        var usedInCareer = await _unitOfWork.CareerProfiles.AnyAsync(
            c => c.ResumeFileId == fileId, cancellationToken);

        if (usedInCareer)
            throw new ConflictException(
                "Файл используется как резюме в карьерном профиле.",
                "file.in_use_career");
    }

    private async Task SafeDeleteFromStorageAsync(string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(storagePath, cancellationToken);
        }
        catch (Exception ex)
        {
            // Метаданные из БД уже удалены — для пользователя операция успешна.
            // Осиротевший объект в хранилище логируем, но запрос не роняем.
            _logger.LogError(ex, "Не удалось удалить объект из хранилища: {StoragePath}", storagePath);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Оставляем только имя файла: клиент может прислать "../../etc/passwd".
        var name = Path.GetFileName(fileName);

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());

        return string.IsNullOrWhiteSpace(cleaned)
            ? "file"
            : cleaned.Length > 255 ? cleaned[^255..] : cleaned;
    }

    private async Task<StoredFile> LoadOwnedAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var file = await _unitOfWork.Files.Query(asNoTracking: !tracked)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(file, file?.UserId ?? Guid.Empty, userId, nameof(StoredFile), id);
    }
}
