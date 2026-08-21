<div align="center">

# LifeOS

**Full Stack AI SaaS — персональный помощник с машинным обучением**

Цели · Финансы · Здоровье · Учёба · Карьера — в одном пространстве,
с AI-аналитикой, которая честно сообщает свою уверенность.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-strict-3178C6?logo=typescript&logoColor=white)
![FastAPI](https://img.shields.io/badge/FastAPI-0.115-009688?logo=fastapi&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

</div>

---

## Скриншоты

<!-- Положи изображения в docs/screenshots/ и раскомментируй -->
<!--
| Dashboard | AI-прогноз |
|---|---|
| ![Dashboard](docs/screenshots/dashboard.png) | ![AI](docs/screenshots/ai-forecast.png) |

| Финансы | Учебный модуль |
|---|---|
| ![Finance](docs/screenshots/finance.png) | ![Study](docs/screenshots/study.png) |
-->

> Скриншоты будут добавлены в `docs/screenshots/`.

---

## О проекте

Человек ведёт задачи в одном приложении, финансы во втором, здоровье
в третьем. Данные не связаны, и никто не видит картину целиком.

LifeOS собирает их в одном месте и применяет машинное обучение:
прогноз расходов, оценка самочувствия, конспект учебного материала,
разбор резюме.

**Принципиальная особенность:** ни один ответ модели не отдаётся без
`confidence`, `explanation` и вклада признаков. Если модель не уверена —
она сообщает об этом прямо, а не выдаёт правдоподобную догадку.

---

## Архитектура

Service-Oriented Architecture: API Gateway + AI-микросервис.

```
        React SPA (Vercel)
              │  HTTPS + Bearer JWT
              ▼
     ASP.NET Core 8 (Render)  ──────►  PostgreSQL (Neon)
       Clean Architecture     ──────►  Firebase Storage
              │
              │  X-Internal-Api-Key
              ▼
        FastAPI (Render)  ──►  scikit-learn + PyTorch
```

**Инварианты системы:**

1. Фронтенд общается **только** с бэкендом и не знает о существовании AI-сервиса.
2. AI-сервис не публикуется в интернет, не хранит пользователей, не подключается к БД.
3. База одна, и владеет ею бэкенд.

> Изначально в документации архитектура называлась «микросервисы».
> На этапе проектирования название исправлено на SOA: в настоящих
> микросервисах у каждого сервиса своя база данных.

---

## Технологии

| Слой | Стек |
|---|---|
| **Frontend** | React 19, TypeScript (strict), Tailwind CSS 4, TanStack Query, Vite, zod |
| **Backend** | ASP.NET Core 8, EF Core, Clean Architecture, JWT + refresh с ротацией, FluentValidation, AutoMapper, Serilog |
| **AI** | FastAPI, scikit-learn, PyTorch, pandas, pytest |
| **Данные** | PostgreSQL (Neon), Firebase Storage |
| **Инфраструктура** | Docker, GitHub Actions, Render, Vercel |

---

## Модули

| Модуль | Возможности |
|---|---|
| **Auth** | Регистрация, вход, JWT + refresh с ротацией и обнаружением кражи |
| **Dashboard** | Сводка по всем модулям одним запросом, 8 виджетов |
| **Цели и задачи** | CRUD, приоритеты, дедлайны, прогресс по задачам |
| **Финансы** | Доходы и расходы, сводка по категориям, **AI-прогноз расходов** |
| **Здоровье** | Дневник сна, шагов, воды, настроения, **AI-оценка самочувствия** |
| **Учёба** | Загрузка PDF, **AI-конспект**, генерация и проверка тестов, заметки |
| **Карьера** | Профиль, загрузка резюме, **AI-разбор резюме** |
| **AI-ассистент** | Лента рекомендаций, аудит обращений к моделям |

---

## Машинное обучение

| Модель | Алгоритм | Задача | Качество |
|---|---|---|---|
| `finance_model` | GradientBoostingRegressor | Прогноз расходов на месяц | R² = 0.93, MAE ≈ 12 000 |
| `health_model` | RandomForest + StandardScaler | Прогноз настроения (1–5) | Accuracy = 0.86, F1 = 0.86 |
| `wellbeing_model` | PyTorch MLP (3→32→16→1) | Интегральная оценка 0–100 | MAE = 6.1, RMSE = 8.9 |

Метрики получены на отложенной выборке (20% данных, не участвовали в обучении).

**Единый формат ответа:**

```json
{
  "result": { "predicted_expense": 198450.0, "trend": "rising", "top_category": "Еда" },
  "confidence": 0.92,
  "is_confident": true,
  "explanation": "Расходы растут по сравнению со средним за период...",
  "contributions": [
    { "feature": "Расход прошлого месяца", "value": 180000.0, "impact": 0.9163 }
  ],
  "model_version": "finance-gbr-1.0"
}
```

Уверенность **вычисляется**, а не задаётся константой: для регрессии —
из разброса ошибки, зафиксированного при обучении, для классификации —
из вероятности выбранного класса.

**Работа без внешнего LLM.** Ключ языковой модели необязателен.
Без него конспект строится извлекающим методом (TF-IDF выбирает
предложения исходного текста — выдумать содержание физически невозможно),
резюме разбирается эвристикой, а генерация тестов честно возвращает
`source: "unavailable"` вместо бессмысленных вопросов.

---

## Безопасность

- **BCrypt** (work factor 12) — пароль в открытом виде не хранится нигде
- **Ротация refresh-токенов** — повторное использование гасит всю цепочку, кража обнаруживается
- **Защита от IDOR** — чужая запись возвращает `404`, а не `403`: перебором нельзя выяснить существующие идентификаторы
- **Одинаковая ошибка** при неверном email и пароле — защита от user enumeration
- **Трёхуровневая проверка файлов** — MIME → расширение → сигнатура по первым байтам
- **Rate limiting** — 5 попыток входа за 5 минут, отдельный лимит на AI-эндпоинты
- **Внутренний ключ** для канала backend → AI, сравнение через `hmac.compare_digest`

---

## Быстрый старт

### Всё в Docker

```bash
git clone https://github.com/<username>/LifeOS.git
cd LifeOS
cp .env.example .env
```

Заполни в `.env` два ключа:

```bash
openssl rand -base64 48                                  # → JWT_KEY
python -c "import secrets; print(secrets.token_urlsafe(48))"  # → INTERNAL_API_KEY
```

```bash
docker compose up --build
```

| Сервис | Адрес |
|---|---|
| Фронтенд | http://localhost:3000 |
| API + Swagger | http://localhost:8080/swagger |
| AI-сервис | внутренняя сеть (наружу не публикуется) |

### Разработка в IDE

<details>
<summary>Развернуть инструкцию</summary>

```bash
# 1. База данных
docker compose up -d postgres

# 2. AI-сервис
cd ai-service
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env                                 # вписать INTERNAL_API_KEY

python -m app.ml.training.generate_datasets
python -m app.ml.training.train_finance
python -m app.ml.training.train_health
python -m app.ml.training.train_wellbeing_torch

uvicorn app.main:app --reload --port 8000

# 3. Backend
cd backend/src/LifeOS.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=lifeos;Username=lifeos;Password=..."
dotnet user-secrets set "Jwt:Key" "<ключ>"
dotnet user-secrets set "AiService:InternalApiKey" "<тот же INTERNAL_API_KEY>"
dotnet run

# 4. Frontend
cd frontend
npm install && npm run dev
```

</details>

---

## Тесты

```bash
cd backend    && dotnet test     # xUnit + Testcontainers (нужен запущенный Docker)
cd frontend   && npx vitest run  # Vitest + Testing Library
cd ai-service && pytest          # pytest
```

Все три набора запускаются автоматически в CI на каждый push.

Интеграционные тесты работают против **настоящего PostgreSQL** в контейнере,
а не против провайдера InMemory: последний выполняет запросы в памяти
и пропустил бы выражения, которые Npgsql не смог бы перевести в SQL.

---

## Структура репозитория

```
LifeOS/
├── backend/                    # ASP.NET Core 8, Clean Architecture
│   ├── src/
│   │   ├── LifeOS.Domain/         # Сущности, enum'ы, доменные исключения
│   │   ├── LifeOS.Application/    # Бизнес-логика, DTO, интерфейсы, валидаторы
│   │   ├── LifeOS.Infrastructure/ # EF Core, репозитории, Firebase, JWT, AI-клиент
│   │   └── LifeOS.API/            # Контроллеры, middleware, конфигурация
│   └── tests/                     # Unit + интеграционные тесты
├── frontend/                   # React 19 + TypeScript
├── ai-service/                 # FastAPI + ML
│   └── app/ml/training/           # Скрипты обучения моделей
├── docs/                       # Документация по фазам + PROJECT_STATE
├── .github/workflows/          # CI
├── docker-compose.yml
└── render.yaml
```

---

## Документация

| Документ | Содержание |
|---|---|
| [`docs/PROJECT_STATE.md`](docs/PROJECT_STATE.md) | Состояние проекта и **~140 архитектурных решений с обоснованиями** |
| `docs/PHASE1..10_*.md` | Разбор каждой фазы: что сделано, почему и как проверить |
| `backend/src/LifeOS.API/LifeOS.API.http` | 67 готовых сценариев проверки API |
| `/swagger` | Интерактивная документация API (в Development) |

Каждое архитектурное решение задокументировано вместе с отвергнутой
альтернативой — почему сделано именно так, а не иначе.

---

## Развёртывание

| Компонент | Платформа |
|---|---|
| Фронтенд | Vercel (`frontend/vercel.json`) |
| Backend + AI | Render (`render.yaml`, Blueprint) |
| База данных | Neon (serverless PostgreSQL) |
| Файлы | Firebase Storage |

Подробная инструкция — в [`docs/PHASE10_DEPLOYMENT.md`](docs/PHASE10_DEPLOYMENT.md).

---

## Лицензии зависимостей

Все библиотеки распространяются под разрешительными лицензиями
(MIT, Apache 2.0, BSD). Специально проверено: PdfPig вместо iText,
FluentAssertions зафиксирован на ветке 6.x (с 8.0 библиотека стала
коммерческой).

---

<div align="center">

**Дипломный проект · 2026**

</div>
