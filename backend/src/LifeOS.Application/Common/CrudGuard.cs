using LifeOS.Domain.Common;
using LifeOS.Domain.Exceptions;

namespace LifeOS.Application.Common;

/// <summary>
/// Проверка владения ресурсом — защита от IDOR (Insecure Direct Object Reference).
///
/// Без неё любой аутентифицированный пользователь мог бы подставить чужой Id
/// в URL и прочитать или удалить чужие данные. Именно эта проверка, а не JWT,
/// отвечает на вопрос «а почему я не вижу цели другого пользователя?».
/// </summary>
public static class CrudGuard
{
    /// <summary>
    /// Возвращает сущность, если она существует и принадлежит пользователю.
    /// Чужая сущность даёт 404, а не 403: иначе по коду ответа можно было бы
    /// выяснить, какие Id существуют в системе.
    /// </summary>
    public static T EnsureOwned<T>(T? entity, Guid ownerId, Guid currentUserId, string entityName, Guid requestedId)
        where T : BaseEntity
    {
        if (entity is null || ownerId != currentUserId)
            throw new NotFoundException(entityName, requestedId);

        return entity;
    }
}
