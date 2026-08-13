# Фаза 1 — Backend Core

Что сделано, как это запустить и как убедиться, что всё работает.

---

## 1. Что входит в фазу

| Пункт плана | Статус |
|---|---|
| Solution + 4 проекта (Clean Architecture) | ✅ |
| Domain Entities (13) + Enums (6) + доменные исключения | ✅ |
| AppDbContext + 13 EF-конфигураций + аудит-интерцептор | ✅ |
| Repository Pattern + Unit of Work + DI | ✅ |
| Global Exception Middleware + Serilog + Swagger + CORS + HealthCheck | ✅ |
| Первая миграция | ⬜ создаётся командой (см. §4) |

Аутентификация, DTO, валидация и CRUD-контроллеры — **сознательно не входят**: это Фазы 2–3.

---

## 2. Зависимости слоёв

```
LifeOS.Domain          ← ни от чего не зависит
      ↑
LifeOS.Application     ← знает только Domain
      ↑
LifeOS.Infrastructure  ← знает Application (реализует его интерфейсы)
      ↑
LifeOS.API             ← знает Infrastructure + Application
```

Правило, которое нельзя нарушать: **стрелки только вверх**. Если Domain начнёт ссылаться на EF Core — Clean Architecture сломана.

---

## 3. Первый запуск

### 3.1 Поднять PostgreSQL локально

```bash
cp .env.example .env      # и заменить пароль
docker compose up -d postgres
```

Либо создать проект в Neon и взять строку подключения оттуда.

### 3.2 Положить строку подключения в user-secrets

Строка подключения **не должна попадать в appsettings.json** — там она будет закоммичена в git.

```bash
cd backend/src/LifeOS.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=lifeos;Username=lifeos;Password=<твой_пароль>"
```

Для Neon:
```
Host=<host>.neon.tech;Database=lifeos;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true
```

### 3.3 Восстановить пакеты и собрать

```bash
cd backend
dotnet restore
dotnet build
```

---

## 4. Первая миграция

```bash
# из папки backend/
dotnet tool install --global dotnet-ef      # один раз на машину
dotnet ef migrations add InitialCreate \
  --project src/LifeOS.Infrastructure \
  --startup-project src/LifeOS.API \
  --output-dir Data/Migrations
```

Применение: при запуске в Development миграции накатываются автоматически (`ApplyMigrationsAsync`). Вручную:

```bash
dotnet ef database update --project src/LifeOS.Infrastructure --startup-project src/LifeOS.API
```

---

## 5. Проверка (чек-лист)

- [ ] `dotnet build` — 0 ошибок
- [ ] `dotnet run --project src/LifeOS.API` стартует, в консоли Serilog пишет «Запуск LifeOS.API...»
- [ ] Открывается `https://localhost:7001/swagger`
- [ ] `GET /api/health` → 200 и JSON со статусом `healthy`
- [ ] `GET /health` → `Healthy` (значит, подключение к PostgreSQL живое)
- [ ] В БД появились 13 таблиц + `__EFMigrationsHistory`
- [ ] В `Users` есть уникальный индекс по `Email`
- [ ] У `Tasks.GoalId` — `ON DELETE SET NULL`
- [ ] В папке `backend/src/LifeOS.API/logs/` появился файл лога

Быстрая проверка схемы в psql:

```sql
\dt
\d "Tasks"
\d "Transactions"
SELECT indexname FROM pg_indexes WHERE schemaname = 'public' ORDER BY 1;
```

---

## 6. Решения, принятые в этой фазе

| Решение | Почему |
|---|---|
| Класс `TaskItem`, таблица `Tasks` | `Task` конфликтует с `System.Threading.Tasks.Task` |
| Класс `StoredFile`, таблица `Files` | `File` конфликтует с `System.IO.File` |
| Enum'ы хранятся строками | Читаемая БД; вставка нового значения не ломает данные |
| `DateOnly` для `Transaction.Date` и `HealthLog.Date` | Это дата без времени → тип `date`, нет проблем с часовыми поясами |
| `CreatedAt`/`UpdatedAt` — интерцептор | Аудит нельзя подделать из DTO и нельзя забыть проставить |
| `Files → StudyMaterials` = `NoAction`, а не `Restrict` | `Restrict` падает при каскадном удалении пользователя (два каскадных пути); `NoAction` откладывает проверку до конца оператора и даёт ту же защиту |
| `HealthLogs (UserId, Date)` — уникальный индекс | Одна запись в день, иначе временной ряд для AI неоднозначен |
| Repository не вызывает `SaveChanges` | Транзакционная граница принадлежит Unit of Work |
| `EnableRetryOnFailure` | Neon — облако, сетевые сбои штатны |
| Автомиграции только в Development | В проде несколько инстансов гонялись бы за схему |

---

## 7. Что дальше — Фаза 2 (Auth)

1. `BCrypt.Net-Next` → `IPasswordHasher`
2. `JwtTokenGenerator` + настройки `Jwt:*`
3. `AuthService`: register / login / refresh с ротацией / logout
4. `CurrentUserService` — реализация `ICurrentUserService` поверх `HttpContext`
5. `AuthController` + DTO + FluentValidation
6. `app.UseAuthentication()` (место уже помечено комментарием в `Program.cs`)
