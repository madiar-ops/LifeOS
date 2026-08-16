using LifeOS.Application.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Files;

public class FileQueryParams : PaginationParams
{
    public ModuleType? Module { get; set; }
}
