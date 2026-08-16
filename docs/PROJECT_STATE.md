# PROJECT_STATE.md

**Project:** LifeOS
**Version:** 1.4
**Last updated:** Phase 4 — Files (код сгенерирован, ожидает проверки)

---

## Current Phase
**Phase 4 — Files** 🟡 КОД ГОТОВ, ТРЕБУЕТ ПРОВЕРКИ

Следующая фаза: **Phase 5 — AI Service (FastAPI)**.

---

## Completed

### Phase 0 — Foundation / Design ✅
- [x] Анализ всей документации (MASTER_GUIDE, TECHNICAL_SPEC, PROMPTS_GUIDE)
- [x] Критический аудит архитектуры (9 проблем найдено и решено)
- [x] Финальная архитектура утверждена (SOA / API Gateway + AI-микросервис)
- [x] Структура монорепо, .NET Solution, React, FastAPI
- [x] ER-диаграмма (13 таблиц) + связи + правила удаления
- [x] Полный список API endpoints
- [x] Development Plan (11 фаз) + порядок реализации модулей (14 шагов)

### Phase 1 — Backend Core ✅ ПОДТВЕРЖДЕНО
- [x] `LifeOS.sln` + 4 проекта Clean Architecture + `Directory.Build.props`
- [x] Domain: `BaseEntity`, `IAuditableEntity`, 13 сущностей, 6 enum'ов, 5 доменных исключений
- [x] Application: `Result<T>`, `Error`, `PagedResult<T>`, `PaginationParams`
- [x] Application: `IGenericRepository<T>`, `IUserRepository`, `IRefreshTokenRepository`, `IUnitOfWork`, `IDateTimeProvider`, `ICurrentUserService`
- [x] Infrastructure: `AppDbContext`, 13 EF-конфигураций, `AuditableEntityInterceptor`, `DateTimeProvider`
- [x] Infrastructure: `GenericRepository<T>`, `UserRepository`, `RefreshTokenRepository`, `UnitOfWork`
- [x] Infrastructure DI: Npgsql + retry, health check на БД
- [x] API: `Program.cs` (Serilog), `GlobalExceptionMiddleware` (ProblemDetails), Swagger + JWT-схема, CORS из конфигурации, `HealthController`, авто-миграции в Development
- [x] Корень репозитория: `.gitignore`, `.env.example`, `docker-compose.yml` (PostgreSQL)
- [x] `dotnet build` пройден локально
- [x] Миграция `InitialCreate` создана и применена (таблицы в БД видны)
- [x] Swagger открывается, `GET /api/health` → 200 `healthy`
- [x] `GET /health` → `Healthy` (подключение к PostgreSQL подтверждено)

### Phase 2 — Auth ✅
- [x] Application: `JwtSettings`, 5 DTO, `IPasswordHasher`, `IJwtTokenGenerator`, `IAuthService`
- [x] Application: `AuthService` (register / login / refresh с ротацией / logout / me)
- [x] Application: 3 валидатора FluentValidation, регистрация с `ValidateOnStart`
- [x] Infrastructure: `BCryptPasswordHasher` (work factor 12), `JwtTokenGenerator`
- [x] API: `CurrentUserService`, `AuthenticationExtensions`, `ValidationFilter`, `AuthController`
- [x] API: `app.UseAuthentication()`, секция `Jwt` в appsettings, `LifeOS.API.http` с 10 сценариями
- [ ] `Jwt:Key` задан в user-secrets
- [ ] Чек-лист проверки из `docs/PHASE2_AUTH.md` §5 пройден

### Phase 3 — Core CRUD ✅
- [x] Application: 21 DTO (Users, Goals, Tasks, Finance, Health, Common)
- [x] Application: `IUserService`, `IGoalService`, `ITaskService`, `IFinanceService`, `IHealthLogService` + реализации
- [x] Application: `MappingProfile` (AutoMapper), 10 валидаторов, `CrudGuard`, `PagedResponse<T>`
- [x] API: `UsersController`, `GoalsController`, `TasksController`, `FinanceController`, `HealthController`
- [x] API: liveness переехал на `PingController` (`/api/ping`), enum'ы как строки в JSON, `DateOnly` в Swagger
- [x] `ValidationFilter` теперь обрабатывает и ошибки привязки модели
- [x] `LifeOS.API.http` дополнен сценариями 11–30
- [ ] Чек-лист проверки из `docs/PHASE3_CORE_CRUD.md` §5 пройден

### Phase 4 — Files 🟡
- [x] Application: `FileStorageSettings`, `FileUploadData`, `StorageUploadResult`, `FileValidationRules`
- [x] Application: `IFileStorageService`, `IFileService`, `FileService` (валидация + компенсация при сбое)
- [x] Infrastructure: `FirebaseStorageService`, `LocalFileStorageService`, `StoragePathBuilder`
- [x] API: `FilesController`, `PUT /api/users/avatar`, `UseStaticFiles`, лимит тела 15 МБ
- [x] Трёхуровневая валидация: MIME → расширение → сигнатура файла
- [x] `LifeOS.API.http` дополнен сценариями 31–41
- [ ] Настроен Firebase (`FileStorage:Bucket` + credentials) ИЛИ осознанно используется локальный провайдер
- [ ] Чек-лист проверки из `docs/PHASE4_FILES.md` §5 пройден
- [ ] Подтверждена защита от подделки типа (переименованный .txt → 400)

---

## Key Architectural Decisions (ADR)

### Phase 0
1. **Архитектура:** честная **SOA / Gateway + AI-микросервис** (одна БД во владении ASP.NET).
2. **AI-стек:** scikit-learn для табличных задач + 1 показательная PyTorch-модель.
3. **Безопасность AI-канала:** FastAPI не публичен, internal API-Key.
4. **Finance:** единая таблица `Transactions` (Income/Expense).
5. **Study:** `StudyMaterials` + `StudyNotes` + `Quizzes`.
6. **Tasks:** `UserId` обязателен, `GoalId` опционален.
7. **Auth:** refresh-токены с ротацией (`IsRevoked` + `ReplacedByToken`).
8. **PK:** UUID на всех таблицах, генерируется приложением.
9. **Backend:** Clean Architecture (Domain / Application / Infrastructure / API).

### Phase 1
10. **Именование:** `Task` → `TaskItem`, `File` → `StoredFile` (конфликт с BCL). Имена таблиц в БД не изменились.
11. **Enum'ы в БД — строками** (`HasConversion<string>`): читаемость + устойчивость к вставке новых значений.
12. **`DateOnly`** для `Transaction.Date` и `HealthLog.Date` → тип `date`, нет проблем с часовыми поясами.
13. **Аудит через `SaveChangesInterceptor`**, а не вручную в сервисах. `CreatedAt` защищён от перезаписи при UPDATE.
14. **`Files → StudyMaterials/CareerProfiles` = `NoAction`**, а не `Restrict`: `Restrict` падает при каскадном удалении пользователя (два каскадных пути), `NoAction` откладывает проверку до конца оператора.
15. **`HealthLogs (UserId, Date)` — уникальный индекс:** одна запись в день.
16. **Репозитории не вызывают `SaveChanges`** — транзакционная граница принадлежит `IUnitOfWork`.
17. **Чтение по умолчанию `AsNoTracking`**, отслеживание — только там, где сущность действительно меняется.
18. **Автомиграции только в Development**; в проде — отдельный шаг деплоя.
19. **`EnableRetryOnFailure(3)`** — Neon облачный, сетевые сбои штатны.
20. **Ошибки в формате ProblemDetails (RFC 7807)** + машиночитаемый `code` для фронтенда. Stack trace — только в Development.
21. **Секреты — в user-secrets (dev) и переменных окружения (prod)**, `appsettings.json` не содержит ни строку подключения, ни `Jwt:Key`.

### Phase 2
22. **Access-токен 15 мин, refresh 7 дней.** Access нельзя отозвать — только пережить, поэтому срок короткий.
23. **Refresh-токен — не JWT, а 64 случайных байта (Base64Url).** Подлинность проверяется только по записи в БД → подделать невозможно, отозвать можно.
24. **Обнаружение повторного использования refresh-токена** → отзыв всей цепочки токенов пользователя (`auth.token_reuse_detected`).
25. **`ClockSkew = TimeSpan.Zero`** — дефолтные 5 минут запаса составляли бы треть жизни access-токена.
26. **Одинаковая ошибка при неверном email и неверном пароле** — защита от user enumeration.
27. **BCrypt work factor 12** — компромисс между стойкостью к перебору и скоростью логина.
28. **Валидация через `ValidationFilter`**, штатная `ModelState` подавлена (`SuppressModelStateInvalidFilter`) — один формат ошибки вместо двух.
29. **`ValidateOnStart` для настроек JWT** — приложение отказывается стартовать без валидного ключа.
30. **Заголовок `X-Token-Expired`** при истечении access-токена — фронт отличает «нужен refresh» от «нужно разлогинить».

### Phase 3
31. **`CrudGuard.EnsureOwned` в каждом сервисе** — защита от IDOR. Фильтр по `UserId` применяется первым в любом запросе.
32. **Чужая сущность возвращает 404, а не 403** — иначе по коду ответа можно перебирать существующие Id.
33. **AutoMapper только Entity → DTO.** Обратно — вручную: клиент физически не может переписать `UserId`, `CreatedAt` или `Role`.
34. **Enum'ы в JSON сериализуются строками** (`JsonStringEnumConverter`).
35. **`Transaction.Amount` всегда положительна** (`Math.Abs`), знак несёт `Type`. Верхняя граница 999 999 999 отсекает опечатки.
36. **Финансовая сводка считается в одной валюте** — конвертации курсов в MVP нет.
37. **Агрегация `GroupBy` выполняется в PostgreSQL**, не в памяти приложения.
38. **Дата записи здоровья не редактируется** — часть уникального ключа `(UserId, Date)`.
39. **`ToLower().Contains()` вместо `EF.Functions.ILike`** — Application не знает, какая СУБД под ним.
40. **Осознанный компромисс:** `LifeOS.Application` ссылается на `Microsoft.EntityFrameworkCore` ради `IQueryable`/`Include` (как в шаблоне Джейсона Тейлора). Провайдер-специфичных пакетов (Npgsql) там нет, `Domain` полностью чист.
41. **Liveness-эндпоинт переехал** с `/api/health` на `/api/ping` — маршрут занял модуль Health.

### Phase 4
42. **Два провайдера хранилища**, выбор по конфигурации: Firebase (прод) и локальная папка (разработка). Локальный не годится для прода — ФС на Render эфемерна.
43. **Проверка сигнатуры файла (магические числа)** — `Content-Type` подделывается тривиально, сверка первых байтов не даёт залить исполняемый файл под видом PDF.
44. **Три уровня валидации:** MIME-тип → расширение → сигнатура. Разрешённые типы различаются по модулям (Study/Career — только PDF).
45. **Имя файла в хранилище заменяется на GUID**, схема пути `users/{userId}/{module}/{guid}` — нет коллизий и path traversal.
46. **`StoragePath` хранится отдельно от `Url` и наружу не отдаётся** — удаление идёт по внутреннему пути.
47. **Компенсация при сбое:** если метаданные не легли в БД, файл удаляется из хранилища — иначе копятся «сироты».
48. **Старый аватар удаляется после коммита транзакции**, а не до — иначе при откате профиль ссылался бы в пустоту.
49. **Ошибка удаления из хранилища не роняет запрос** — логируется, для пользователя операция успешна.
50. **Проверка ссылок перед удалением файла → 409** — у `Files` в БД стоит `NoAction`, иначе была бы ошибка внешнего ключа.
51. **`FileUploadData` вместо `IFormFile`** в слое Application — сервис вызываем из фонового задания или теста.

---

## Database Tables (13)
Users, RefreshTokens, Goals, Tasks, Transactions, HealthLogs,
StudyMaterials, StudyNotes, Quizzes, CareerProfiles,
Recommendations, AIHistory, Files

---

## Not Started
- [ ] Phase 5 — AI Service (FastAPI + модели)
- [ ] Phase 6 — AI-модули (Study, Career, analysis)
- [ ] Phase 7 — Dashboard API
- [ ] Phase 8 — Frontend (React)
- [ ] Phase 9 — Integration & Testing
- [ ] Phase 10 — Deployment
- [ ] Phase 11 — Defense Prep

---

## Open Questions
- **Валюта по умолчанию:** `KZT` (зафиксировано в `FinanceService.DefaultCurrency`).
- **Мультивалютность:** решено — сводка считается в рамках одной валюты, параметр `currency` в `/finance/summary`. Конвертация курсов вне MVP.
- **Тестовые проекты:** `LifeOS.UnitTests` / `LifeOS.IntegrationTests` в Solution пока не добавлены — планово в Фазе 9. Можно добавить раньше, если хочется писать тесты параллельно.

---

## Tooling Decision
- **IDE:** Visual Studio 2022 — для `backend/`, VS Code — для `frontend/` и `ai-service/`.
- **Репозиторий:** монорепо `LifeOS` на GitHub, стратегия ветка-на-фазу.
- **Целевой фреймворк:** .NET 8 (LTS) — поддержка до ноября 2026, гарантированно доступен на Render.

---

## Next Action
1. Собрать проект (`dotnet build`) — добавился пакет `Google.Cloud.Storage.V1`.
2. Настроить Firebase (`docs/PHASE4_FILES.md` §2) либо оставить локальный провайдер для разработки.
3. Пройти чек-лист `docs/PHASE4_FILES.md` §5 — загрузку удобнее проверять через Swagger UI.
4. Запустить **Фазу 5 — AI Service (FastAPI)**.
