# LifeOS AI Service

AI-микросервис платформы LifeOS. Отвечает **только за инференс**: не хранит
пользователей, не знает про JWT, не подключается к PostgreSQL и не общается
с React. Единственный клиент — ASP.NET Core по внутреннему ключу.

---

## Быстрый старт

```bash
cd ai-service

python -m venv .venv
# Windows:
.venv\Scripts\activate
# Linux/macOS:
source .venv/bin/activate

pip install -r requirements.txt

cp .env.example .env
# сгенерировать ключ и вписать в .env:
python -c "import secrets; print(secrets.token_urlsafe(48))"
```

### Обучение моделей (обязательно перед первым запуском)

```bash
python -m app.ml.training.generate_datasets      # ~10 секунд
python -m app.ml.training.train_finance          # ~5 секунд
python -m app.ml.training.train_health           # ~10 секунд
python -m app.ml.training.train_wellbeing_torch  # ~40 секунд
```

Артефакты появятся в `app/ml/artifacts/`. В git они не коммитятся —
воспроизводятся командами выше.

### Запуск

```bash
uvicorn app.main:app --reload --port 8000
```

Swagger: http://localhost:8000/docs (только в Development).

### Тесты

```bash
pytest
```

---

## Модели

| Модель | Алгоритм | Задача | Метрика |
|---|---|---|---|
| `finance_model` | GradientBoostingRegressor | Прогноз расходов на месяц | R² ≈ 0.93, MAE ≈ 12 000 |
| `health_model` | RandomForest + StandardScaler | Прогноз настроения (1–5) | Accuracy ≈ 0.86 |
| `wellbeing_model` | PyTorch MLP (3-32-16-1) | Интегральная оценка 0–100 | MAE ≈ 6.1 |

Метрики получены на отложенной выборке (20%), на синтетических данных.

---

## Endpoints

| Метод | Путь | Ключ | Назначение |
|---|---|---|---|
| GET | `/ping` | нет | Живость |
| GET | `/health` | нет | Какие модели загружены |
| POST | `/finance/analysis` | да | Прогноз расходов |
| POST | `/health-analysis` | да | Оценка самочувствия |
| POST | `/study/summary` | да | Конспект материала |
| POST | `/study/quiz` | да | Генерация теста (нужен LLM) |
| POST | `/career/resume-analysis` | да | Разбор резюме |

Заголовок: `X-Internal-Api-Key: <ключ>`

---

## Формат ответа

Каждый ответ модели обёрнут в единую структуру:

```json
{
  "result": { },
  "confidence": 0.91,
  "is_confident": true,
  "explanation": "Расходы держатся на стабильном уровне...",
  "contributions": [
    { "feature": "Расход прошлого месяца", "value": 180000.0, "impact": 0.9163 }
  ],
  "model_version": "finance-gbr-1.0"
}
```

`confidence` и `is_confident` присутствуют **всегда** — это требование
MASTER_GUIDE: «AI никогда не генерирует случайные ответы. Если AI не уверен —
он сообщает об этом».

---

## Работа без внешнего LLM

`LLM_API_KEY` необязателен. Без него:

| Модуль | Поведение |
|---|---|
| Study / summary | Извлекающая суммаризация (TF-IDF), `source: "extractive"` |
| Study / quiz | Честный отказ, `source: "unavailable"` |
| Career | Эвристический разбор по структуре, `source: "heuristic"` |

Проект полностью запускается и демонстрируется без платных сервисов.

---

## Замена синтетических данных на Kaggle

Скрипты обучения читают CSV из `app/ml/datasets/`. Чтобы использовать
реальные данные, положите файл с теми же колонками:

- `finance_history.csv`: `income`, `prev_expense`, `expense_ratio_prev`, `month_of_year`, `expense`
- `health_logs.csv`: `sleep_hours`, `steps`, `water_ml`, `mood`

и перезапустите обучение. Менять код не нужно.
