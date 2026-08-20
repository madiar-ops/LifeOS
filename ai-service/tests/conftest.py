"""Общая настройка тестов."""

import os

import pytest

# Ключ выставляется ДО импорта приложения: настройки кэшируются
# при первом обращении, и позднее изменение окружения уже не подхватится.
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-key")
os.environ.setdefault("ENVIRONMENT", "Development")

from fastapi.testclient import TestClient  # noqa: E402

from app.config import get_settings  # noqa: E402
from app.main import app  # noqa: E402

VALID_HEADERS = {"X-Internal-Api-Key": "test-internal-key"}

# Минимально валидные тела запросов ко всем эндпоинтам с моделями.
# Используются там, где проверяется не содержание ответа, а инвариант,
# общий для всего сервиса: конверт AIResponse, защита ключом, валидация.
SAMPLE_TEXT = (
    "Машинное обучение это раздел искусственного интеллекта. "
    "Алгоритмы обучаются на данных и выявляют закономерности. "
    "Существует обучение с учителем и обучение без учителя. "
    "Качество модели оценивают на отложенной выборке."
)

AI_ENDPOINTS: dict[str, dict] = {
    "/finance/analysis": {
        "history": [
            {"month": "2026-06", "income": 250000, "expense": 175000},
            {"month": "2026-07", "income": 250000, "expense": 180000},
        ],
        "categories": [{"category": "Еда", "amount": 60000}],
        "currency": "KZT",
    },
    "/health-analysis": {
        "entries": [
            {"date": "2026-08-01", "sleep_hours": 7.5, "steps": 9000, "water_ml": 2000, "mood": 4},
            {"date": "2026-08-02", "sleep_hours": 7.2, "steps": 8500, "water_ml": 1900, "mood": 4},
        ]
    },
    "/study/summary": {"text": SAMPLE_TEXT, "max_sentences": 3},
    "/study/quiz": {"text": SAMPLE_TEXT, "question_count": 3},
    "/career/resume-analysis": {
        "resume_text": (
            "Опыт работы: разработчик 3 года. Образование: университет. "
            "Навыки: C#, ASP.NET Core, React. Сократил время отклика на 40%."
        ),
        "desired_position": "Backend Developer",
        "skills": ["C#"],
    },
}


@pytest.fixture(scope="session")
def client():
    # Контекстный менеджер запускает lifespan — без него модели не загрузятся.
    with TestClient(app) as test_client:
        yield test_client


@pytest.fixture(scope="session")
def raw_client():
    """Клиент, который НЕ перевыбрасывает необработанные исключения.

    Нужен там, где проверяется поведение сервиса при сбое: обычный TestClient
    поднимает исключение внутрь теста, и мы не увидим, какой HTTP-ответ
    реально ушёл бы клиенту — а именно он и есть предмет проверки.
    """
    with TestClient(app, raise_server_exceptions=False) as test_client:
        yield test_client


@pytest.fixture
def auth_headers():
    return VALID_HEADERS


@pytest.fixture(scope="session")
def confidence_threshold() -> float:
    """Порог доверия берём из настроек, а не хардкодим: тест должен
    проверять согласованность confidence и is_confident, а не совпадение
    с числом, которое администратор вправе поменять в .env."""
    return get_settings().confidence_threshold
