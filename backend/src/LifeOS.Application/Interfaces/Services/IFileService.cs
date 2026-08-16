using LifeOS.Application.Common;
using LifeOS.Application.DTO.Files;
using LifeOS.Application.Files;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.Interfaces.Services;

public interface IFileService
{
    Task<PagedResult<FileResponse>> GetAllAsync(
        FileQueryParams query, CancellationToken cancellationToken = default);

    Task<FileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FileResponse> UploadAsync(
        FileUploadData upload, ModuleType module, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Загрузка аватара: заменяет предыдущий и обновляет профиль.</summary>
    Task<string> UploadAvatarAsync(FileUploadData upload, CancellationToken cancellationToken = default);
}
