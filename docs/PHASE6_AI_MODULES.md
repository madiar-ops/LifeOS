# Фаза 6 — AI-модули и интеграция

Здесь три сервиса впервые работают вместе: React → ASP.NET Core → FastAPI.

---

## 1. Что добавлено

| Слой | Содержимое |
|---|---|
| **Application** | `AiSettings`, `AiContracts`, `AiEnvelopeExtensions`, `IAiService`, `IDocumentTextExtractor` |
| **Application** | `StudyService`, `CareerService`, `RecommendationService`, `AiHistoryRecorder` |
| **Application** | `AnalyzeAsync` в `FinanceService` и `HealthLogService`, 15 DTO, 6 валидаторов |
| **Infrastructure** | `AiServiceClient` (типизированный HttpClient + Polly), `PdfTextExtractor` (PdfPig) |
| **Infrastructure** | `DownloadAsync` в обоих провайдерах хранилища |
| **API** | `StudyController`, `CareerController`, `RecommendationsController`, `/finance/analysis`, `/health/analysis` |

Миграция **не нужна** — все таблицы созданы в Фазе 1.

---

## 2. Настройка перед запуском

Ключ должен **совпадать** на обеих сторонах:

```bash
# 1. Сгенерировать (если ещё нет)
python -c "import secrets; print(secrets.token_urlsafe(48))"

# 2. AI-сервис: ai-service/.env
INTERNAL_API_KEY=<ключ>

# 3. Backend: user-secrets
cd backend/src/LifeOS.API
dotnet user-secrets set "AiService:InternalApiKey" "<тот_же_ключ>"
dotnet user-secrets set "AiService:BaseUrl" "http://localhost:8000"
```

Без ключа backend **не запустится** (`ValidateOnStart`). Порядок запуска:
сначала `uvicorn app.main:app --port 8000`, потом backend.

---

## 3. Как проходит запрос

```
React
  │  POST /api/study/materials/{id}/summarize   (JWT)
  ▼
ASP.NET Core
  │  1. CrudGuard: материал принадлежит пользователю?
  │  2. Скачивает PDF из хранилища
  │  3. PdfPig извлекает текст
  │  4. POST /study/summary  (X-Internal-Api-Key)
  ▼
FastAPI
  │  проверка ключа → инференс → AIResponse с confidence
  ▼
ASP.NET Core
  │  5. Сохраняет summary в StudyMaterials
  │  6. Пишет вызов в AIHistory
  │  7. При высокой уверенности создаёт Recommendation
  ▼
React  ← результат + confidence + explanation
```

Фронтенд **никогда** не обращается к FastAPI напрямую — это требование MASTER_GUIDE.

---

## 4. Принятые решения

| Решение | Почему |
|---|---|
| **`AiContracts` — отдельные типы, не доменные** | FastAPI живёт своим циклом. Изменится его схема — сломается один файл, а не доменная модель |
| **`JsonNamingPolicy.SnakeCaseLower` политикой, а не атрибутами** | Python отдаёт snake_case. Настраиваем один раз вместо `[JsonPropertyName]` на каждом поле |
| **Ключ и адрес AI знает только `AiServiceClient`** | Единственная точка, где живёт конфигурация канала |
| **`AddStandardResilienceHandler` (Polly)** | Сетевой сбой между двумя сервисами — штатная ситуация, а не повод показать ошибку пользователю |
| **`TotalRequestTimeout` больше `AttemptTimeout`** | Иначе библиотека отклонит конфигурацию при старте — неочевидное требование Polly |
| **Недоступность AI → 400 `ai.unavailable`, не 500** | Это не ошибка в коде backend. Пользователю нужно понятное сообщение, что функция временно недоступна |
| **401 от FastAPI → `ai.unauthorized`** | Рассинхрон ключей — ошибка конфигурации развёртывания. Отдельный код помогает диагностировать |
| **PdfPig, а не iText** | Чистый C# под Apache 2.0, без нативных зависимостей: важно для Linux-контейнера, лицензия совместима с бесплатным использованием |
| **`NearestNeighbourWordExtractor`** | В PDF нет понятия «слово» — только глифы с координатами. Наивное чтение даёт склейку текста |
| **Предел 60 000 символов при извлечении** | AI-сервис всё равно обрезает вход; тащить мегабайты по сети незачем |
| **Скан без текстового слоя → понятная 400** | Распространённый случай. Пользователю нужно объяснение, а не пустой конспект. OCR в проекте не используется |
| **Файл загружается отдельно, материал создаётся из `FileId`** | Study не дублирует валидацию файлов, а переиспользует модуль Files |
| **Один файл — один материал (409 при повторе)** | Иначе один PDF порождал бы несколько конспектов, а удаление файла блокировалось бы неочевидно |
| **Правильные ответы теста не отдаются клиенту** | Иначе тест решается через инструменты разработчика. Проверка только на сервере |
| **Рекомендация создаётся только при `isConfident`** | Лента засорялась бы догадками модели, и пользователь перестал бы ей доверять |
| **`AIHistory` хранит длину текста, а не сам текст** | Полное содержимое учебника в таблице — это и объём, и лишние персональные данные |
| **Payload из `AIHistory` наружу не отдаётся** | Там могут быть фрагменты личных документов |
| **Backend агрегирует данные до отправки в AI** | FastAPI получает помесячные итоги, а не отдельные транзакции: он не имеет доступа к БД и не должен видеть сырые данные |
| **`confidence` проходит насквозь до фронтенда** | Требование MASTER_GUIDE. Потерять его при передаче через backend нельзя — для этого общий `ToResponse` |
| **Карьерный профиль создаётся лениво** | Иначе у каждого пользователя висела бы пустая запись, даже если модулем он не пользовался |
| **`DownloadAsync` добавлен в `IFileStorageService`** | Учебному модулю нужно получить PDF обратно. Изменение интерфейса Фазы 4 — осознанное |

---

## 5. Проверка (чек-лист)

Сценарии 42–62 в `LifeOS.API.http`. **Сначала запусти ai-service.**

**Канал backend ↔ AI**
- [ ] `GET http://localhost:8000/health` → все модели `true`
- [ ] Backend стартует (значит `AiService:InternalApiKey` задан)
- [ ] `GET /api/finance/analysis?monthsBack=6` → **200** с `confidence` и `explanation`
- [ ] **Останови ai-service, повтори запрос → 400 `ai.unavailable`, НЕ 500** ← *ключевая проверка устойчивости*
- [ ] Поставь неверный ключ в user-secrets → **400 `ai.unauthorized`**

**Finance и Health**
- [ ] `/api/finance/analysis` без транзакций → **400 `finance.no_data`**
- [ ] `/api/health/analysis?daysBack=30` → оценка, факторы риска, рекомендации
- [ ] После обоих вызовов `GET /api/recommendations` содержит записи
- [ ] `GET /api/ai/history` показывает вызовы, но **без** payload

**Study**
- [ ] Загрузка PDF → создание материала → **201**
- [ ] Тот же файл повторно → **409 `study.file_already_used`**
- [ ] `POST /materials/{id}/summarize` → конспект, поле `summary` заполнилось
- [ ] PDF-скан без текста → **400 `study.no_text_layer`**
- [ ] `POST /study/quizzes` без ключа LLM → **400 `study.quiz_unavailable`**
- [ ] С ключом LLM: тест создан, в `GET /quizzes/{id}` **нет** `correctIndex`
- [ ] `submit` возвращает оценку и правильные ответы
- [ ] Неверное количество ответов → **400**

**Career**
- [ ] `GET /api/career/profile` создаёт профиль автоматически
- [ ] Привязка не-PDF файла → **400 `career.pdf_required`**
- [ ] `POST /resume-analysis` без резюме → **400 `career.resume_missing`**
- [ ] С резюме → разбор, поле `aiReview` в профиле заполнилось

**Изоляция данных**
- [ ] Чужой материал / тест / заметка по Id → **404**

---

## 6. Что дальше — Фаза 7 (Dashboard API)

Агрегирующий модуль поверх всех написанных: количество задач и целей,
баланс, прогресс целей, последние документы, свежие рекомендации,
статистика по модулям — одним запросом `GET /api/dashboard`.
