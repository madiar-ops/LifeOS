# PROJECT_STATE.md

**Project:** LifeOS
**Version:** 1.7
**Last updated:** Phase 7 — Dashboard API (BACKEND ЗАКРЫТ)

---

## Current Phase
**Phase 7 — Dashboard** 🟡 КОД ГОТОВ, ТРЕБУЕТ ПРОВЕРКИ

Все серверные фазы завершены. Следующая фаза: **Phase 8 — Frontend (React)**.

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

### Phase 4 — Files ✅
- [x] Application: `FileStorageSettings`, `FileUploadData`, `StorageUploadResult`, `FileValidationRules`
- [x] Application: `IFileStorageService`, `IFileService`, `FileService` (валидация + компенсация при сбое)
- [x] Infrastructure: `FirebaseStorageService`, `LocalFileStorageService`, `StoragePathBuilder`
- [x] API: `FilesController`, `PUT /api/users/avatar`, `UseStaticFiles`, лимит тела 15 МБ
- [x] Трёхуровневая валидация: MIME → расширение → сигнатура файла
- [x] `LifeOS.API.http` дополнен сценариями 31–41
- [ ] Настроен Firebase ИЛИ осознанно используется локальный провайдер
- [ ] Чек-лист проверки из `docs/PHASE4_FILES.md` §5 пройден

### Phase 5 — AI Service ✅ ПРОВЕРЕНО ЗАПУСКОМ
- [x] Скелет FastAPI: `config`, `security` (внутренний ключ), `main` с lifespan
- [x] 5 модулей схем, 6 сервисов, 5 роутеров
- [x] Генерация датасетов (9600 + 27000 строк, фиксированный seed)
- [x] `finance_model` (GradientBoosting): **R² = 0.9290**, MAE = 12 036
- [x] `health_model` (RandomForest + Scaler): **Accuracy = 0.8613**, F1 = 0.8580
- [x] `wellbeing_model` (PyTorch MLP): **MAE = 6.139**, RMSE = 8.923
- [x] Единый формат `AIResponse` с `confidence`, `is_confident`, `explanation`, `contributions`
- [x] Запасные локальные алгоритмы для Study и Career (работа без LLM-ключа)
- [x] **18 pytest-тестов проходят**
- [x] Dockerfile (multi-stage, непривилегированный пользователь), README
- [ ] Прогон чек-листа `docs/PHASE5_AI_SERVICE.md` §4 на машине разработчика
- [ ] Сгенерирован и записан `INTERNAL_API_KEY` в `ai-service/.env`

### Phase 6 — AI-модули и интеграция ✅
- [x] Application: `AiSettings`, `AiContracts`, `AiEnvelopeExtensions`, `IAiService`, `IDocumentTextExtractor`
- [x] Application: `StudyService` (материалы, конспект, тесты, заметки)
- [x] Application: `CareerService` (профиль + разбор резюме)
- [x] Application: `AiHistoryRecorder` (аудит + создание рекомендаций), `RecommendationService`
- [x] Application: `AnalyzeAsync` в `FinanceService` и `HealthLogService`
- [x] Infrastructure: `AiServiceClient` (HttpClient + Polly resilience), `PdfTextExtractor` (PdfPig)
- [x] Infrastructure: `DownloadAsync` добавлен в оба провайдера хранилища
- [x] API: `StudyController`, `CareerController`, `RecommendationsController`
- [x] API: `GET /api/finance/analysis`, `GET /api/health/analysis`
- [x] `LifeOS.API.http` дополнен сценариями 42–62
- [ ] `AiService:InternalApiKey` задан и совпадает с `INTERNAL_API_KEY` в ai-service
- [ ] Чек-лист `docs/PHASE6_AI_MODULES.md` §5 пройден

### Phase 7 — Dashboard 🟡
- [x] Application: `DashboardDtos` (14 типов), `IDashboardService`, `DashboardService`
- [x] Восемь виджетов: цели, задачи, финансы, здоровье, учёба, карьера, рекомендации, файлы
- [x] Все агрегаты считаются в PostgreSQL через `GroupBy`
- [x] API: `DashboardController` — `GET /api/dashboard?days=30`
- [x] `LifeOS.API.http` дополнен сценариями 63–67
- [ ] Чек-лист `docs/PHASE7_DASHBOARD.md` §4 пройден
- [ ] Проверено на новом пользователе без данных (должны быть нули, не 500)

**Backend в этой фазе не изменялся** (требование PROMPTS_GUIDE, Prompt 3).

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

### Phase 5
52. **Внутренний ключ `X-Internal-Api-Key`** защищает канал ASP.NET → FastAPI. Сравнение через `hmac.compare_digest` (постоянное время). Пустой ключ → сервис отвечает 500, а не работает «открыто».
53. **Модели загружаются один раз в `lifespan`**, не на каждый запрос — ключевое отличие инференс-сервиса от обучающего скрипта.
54. **Отсутствие модели даёт 503 только своему эндпоинту**, сервис остаётся работоспособным.
55. **`confidence` + `is_confident` + `explanation` в КАЖДОМ ответе** — требование MASTER_GUIDE. Уверенность вычисляется (`residual_std` для регрессии, `predict_proba` для классификации), а не задаётся константой.
56. **`contributions`** — вклад признаков из `feature_importances_`: модель не чёрный ящик.
57. **GradientBoosting для табличных задач, один PyTorch MLP как показательная модель.** Честная позиция: на этих данных бустинг не хуже, PyTorch демонстрирует владение инструментом.
58. **Параметры нормализации сохраняются вместе с весами** (torch) и внутри `Pipeline` (sklearn) — исключает подачу данных в чужом масштабе в проде.
59. **Синтетические данные с фиксированным seed** вместо Kaggle: проект собирается «из коробки». Замена на реальный CSV с теми же колонками не требует правки кода.
60. **Извлекающая суммаризация как запасной путь** — физически не может выдумать содержание.
61. **Отказ вместо плохого результата:** без LLM-ключа генерация тестов возвращает `source: "unavailable"`, а не бессмысленные вопросы.
62. **Падение внешнего LLM не роняет эндпоинт** — срабатывает локальный алгоритм с пониженной уверенностью.
63. **Swagger и CORS отключены вне Development** — сервис приватный.
64. **Артефакты моделей и датасеты не коммитятся** — воспроизводятся четырьмя командами.

### Phase 6
65. **`AiContracts` — отдельные типы, не доменные сущности.** Изменение схемы FastAPI ломает один файл, а не домен.
66. **snake_case задаётся политикой сериализации**, а не атрибутами на каждом поле.
67. **`AddStandardResilienceHandler`** (ретраи + circuit breaker). `TotalRequestTimeout` обязан превышать `AttemptTimeout`, иначе Polly отклоняет конфигурацию при старте.
68. **Недоступность AI → 400 `ai.unavailable`, не 500.** Отдельные коды: `ai.unauthorized` (рассинхрон ключей), `ai.model_unavailable` (модель не обучена), `ai.timeout`.
69. **PdfPig вместо iText** — чистый C#, Apache 2.0, без нативных зависимостей. `NearestNeighbourWordExtractor`, потому что в PDF нет понятия «слово».
70. **Скан без текстового слоя → понятная 400 `study.no_text_layer`.** OCR в проекте не используется.
71. **Файл загружается модулем Files, материал создаётся из `FileId`** — валидация не дублируется. Один файл = один материал (409 при повторе).
72. **Правильные ответы теста не отдаются клиенту** — иначе тест решается через DevTools. Проверка только на сервере.
73. **Рекомендация создаётся только при `isConfident`** и превышении `RecommendationThreshold` — лента не засоряется догадками.
74. **`AIHistory` хранит длину текста, а не сам текст**; payload наружу не отдаётся (личные документы).
75. **Backend агрегирует данные до отправки в AI** — FastAPI получает помесячные итоги, а не сырые транзакции.
76. **`confidence` проходит насквозь до фронтенда** через общий `AiEnvelopeExtensions.ToResponse`.
77. **Карьерный профиль создаётся лениво**, при первом обращении.
78. **`DownloadAsync` добавлен в `IFileStorageService`** — осознанное изменение интерфейса Фазы 4 ради Study и Career.

### Phase 7
79. **Один агрегирующий endpoint вместо сборки на фронтенде** — восемь запросов ради одного экрана означают восемь TLS-рукопожатий и восемь проверок JWT.
80. **Запросы Dashboard идут ПОСЛЕДОВАТЕЛЬНО, не через `Task.WhenAll`** — EF Core не поддерживает конкурентные операции на одном `DbContext`. Готовый ответ на вопрос «почему не распараллелили».
81. **Dashboard не вызывает AI** — экран обязан открываться мгновенно; рекомендации читаются из таблицы.
82. **Отменённые цели исключены из знаменателя `completionRate`** — отмена не провал.
83. **Валюта дашборда — самая частая у пользователя**, смешивание валют исключено.
84. **Тренд всегда за 6 месяцев** независимо от параметра `days` — график из двух точек бессмыслен.
85. **`days` обрезается до 1–365 вместо 400** — дашборд не падает из-за опечатки в query-параметре.

---

## Database Tables (13)
Users, RefreshTokens, Goals, Tasks, Transactions, HealthLogs,
StudyMaterials, StudyNotes, Quizzes, CareerProfiles,
Recommendations, AIHistory, Files

---

## Not Started
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
1. Пройти чек-листы Фаз 6 и 7 (сценарии 42–67 в `LifeOS.API.http`).
2. Закоммитить фазы 6 и 7.
3. Запустить **Фазу 8 — Frontend (React)**.

**Backend завершён:** 7 из 7 серверных фаз.
