using FluentAssertions;
using LifeOS.Application.Common;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Exceptions;

namespace LifeOS.UnitTests.Common;

/// <summary>
/// Тесты защиты от IDOR.
///
/// <see cref="CrudGuard"/> — одна из самых коротких и одновременно самых
/// ответственных частей проекта: именно она, а не JWT, отвечает на вопрос
/// «почему я не вижу чужие цели». Ошибка здесь открывает доступ ко всем
/// данным всех пользователей.
/// </summary>
public class CrudGuardTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Intruder = Guid.NewGuid();

    [Fact]
    public void Своя_сущность_возвращается_как_есть()
    {
        var goal = new Goal { UserId = Owner, Title = "Защитить диплом" };

        var result = CrudGuard.EnsureOwned(goal, goal.UserId, Owner, nameof(Goal), goal.Id);

        result.Should().BeSameAs(goal);
    }

    [Fact]
    public void Чужая_сущность_даёт_404_а_не_403()
    {
        var goal = new Goal { UserId = Owner, Title = "Защитить диплом" };

        var act = () => CrudGuard.EnsureOwned(goal, goal.UserId, Intruder, nameof(Goal), goal.Id);

        // 403 означал бы «объект существует, но он не твой» — по коду ответа
        // можно было бы перебором выяснить, какие идентификаторы заняты.
        // 404 не раскрывает даже факта существования.
        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void Отсутствующая_и_чужая_сущность_неотличимы_по_ответу()
    {
        var id = Guid.NewGuid();
        var foreign = new Goal { Id = id, UserId = Owner, Title = "Чужая цель" };

        var missing = Record.Exception(
            () => CrudGuard.EnsureOwned<Goal>(null, Guid.Empty, Intruder, nameof(Goal), id));
        var notMine = Record.Exception(
            () => CrudGuard.EnsureOwned(foreign, foreign.UserId, Intruder, nameof(Goal), id));

        missing.Should().BeOfType<NotFoundException>();
        notMine.Should().BeOfType<NotFoundException>();
        missing!.Message.Should().Be(notMine!.Message, "иначе разница в тексте выдаёт существование объекта");
    }

    [Fact]
    public void Проверка_работает_для_любой_сущности_домена()
    {
        var task = new TaskItem { UserId = Owner, Title = "Написать тесты" };
        var transaction = new Transaction { UserId = Owner, Category = "Учёба" };

        CrudGuard.EnsureOwned(task, task.UserId, Owner, nameof(TaskItem), task.Id)
                 .Should().BeSameAs(task);

        var act = () => CrudGuard.EnsureOwned(
            transaction, transaction.UserId, Intruder, nameof(Transaction), transaction.Id);

        act.Should().Throw<NotFoundException>();
    }
}
