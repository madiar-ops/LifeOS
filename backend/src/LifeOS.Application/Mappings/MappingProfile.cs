using AutoMapper;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.DTO.Files;
using LifeOS.Application.DTO.Finance;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Health;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Domain.Entities;

namespace LifeOS.Application.Mappings;

/// <summary>
/// Единый профиль маппинга Entity → DTO.
///
/// Маппинг только в одну сторону — из сущности в DTO. Обратное направление
/// (DTO → Entity) делается вручную в сервисах: так видно, какие именно поля
/// клиент вправе задать, и невозможно случайно позволить ему переписать
/// UserId, CreatedAt или Role через лишнее поле в JSON.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserResponse>();

        CreateMap<Goal, GoalResponse>()
            .ForCtorParam(nameof(GoalResponse.TotalTasks),
                opt => opt.MapFrom(src => src.Tasks.Count))
            .ForCtorParam(nameof(GoalResponse.CompletedTasks),
                opt => opt.MapFrom(src => src.Tasks.Count(t => t.Completed)));

        CreateMap<TaskItem, TaskResponse>()
            .ForCtorParam(nameof(TaskResponse.GoalTitle),
                opt => opt.MapFrom(src => src.Goal != null ? src.Goal.Title : null));

        CreateMap<Transaction, TransactionResponse>();

        CreateMap<HealthLog, HealthLogResponse>();

        // Наружу отдаём FirebaseUrl под нейтральным именем Url, а StoragePath
        // не отдаём вовсе: внутреннее устройство хранилища клиента не касается.
        CreateMap<StoredFile, FileResponse>()
            .ForCtorParam(nameof(FileResponse.Url), opt => opt.MapFrom(src => src.FirebaseUrl));
    }
}
