# LifeOS

Full Stack AI SaaS — персональный цифровой помощник, объединяющий учёбу,
финансы, карьеру, здоровье и цели в одном пространстве с AI-аналитикой.

Дипломный проект. Автор: Мадияр.

---

## Архитектура

Service-Oriented Architecture: API Gateway + AI-микросервис.

```
        React (Vercel)
             │  JWT
             ▼
   ASP.NET Core 8 (Render)  ──────►  PostgreSQL (Neon)
     Clean Architecture     ──────►  Firebase Storage
             │
             │  X-Internal-Api-Key
             ▼
     FastAPI (Render)  ──►  scikit-learn + PyTorch
```

Фронтенд **никогда** не обращается к AI-сервису напрямую — только через
backend. AI-сервис не публикуется в интернет, не знает про пользователей
и не подключается к базе данных.

| Слой | Технологии |
|---|---|
| Frontend | React 19, TypeScript (strict), Tailwind CSS 4, TanStack Query, Vite |
| Backend | ASP.NET Core 8, EF Core, Clean Architecture, JWT + refresh с ротацией |
| AI | FastAPI, scikit-learn, PyTorch, pytest |
| Данные | PostgreSQL (Neon), Firebase Storage |
| Инфраструктура | Docker, GitHub Actions, Render, Vercel |

---

## Модули

Аутентификация · Dashboard · Цели · Задачи · Финансы · Здоровье ·
Учёба (PDF → конспект → тесты) · Карьера (разбор резюме) · AI-ассистент ·
Профиль · Настройки

---

## Быстрый старт

### Вариант 1: всё в Docker

```bash
cp .env.example .env
# заполнить JWT_KEY и INTERNAL_API_KEY (команды генерации — в .env.example)

docker compose up --build
```

Фронтенд: http://localhost:3000 · API: http://localhost:8080/swagger

### Вариант 2: разработка в IDE

```bash
# 1. База
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
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
dotnet user-secrets set "Jwt:Key" "..."
dotnet user-secrets set "AiService:InternalApiKey" "тот же INTERNAL_API_KEY"
dotnet run

# 4. Frontend
cd frontend
npm install && npm run dev
```

---

## Тесты

```bash
cd backend    && dotnet test    # xUnit + Testcontainers (нужен Docker)
cd frontend   && npx vitest run # Vitest + Testing Library
cd ai-service && pytest         # pytest
```

Все три набора запускаются автоматически в CI на каждый push.

---

## Документация

| Файл | Содержание |
|---|---|
| `docs/PROJECT_STATE.md` | Текущее состояние, все архитектурные решения (ADR) |
| `docs/PHASE1..10_*.md` | Разбор каждой фазы: что сделано, почему и как проверить |
| `backend/src/LifeOS.API/LifeOS.API.http` | 67 сценариев проверки API |
| `/swagger` | Интерактивная документация API (в Development) |

---

## Лицензии зависимостей

Все использованные библиотеки распространяются под разрешительными
лицензиями (MIT, Apache 2.0, BSD). Специально проверено: PdfPig вместо
iText, FluentAssertions зафиксирован на ветке 6.x.
