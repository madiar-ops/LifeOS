"""Общая настройка тестов."""

import os

import pytest

# Ключ выставляется ДО импорта приложения: настройки кэшируются
# при первом обращении, и позднее изменение окружения уже не подхватится.
os.environ.setdefault("INTERNAL_API_KEY", "test-internal-key")
os.environ.setdefault("ENVIRONMENT", "Development")

from fastapi.testclient import TestClient  # noqa: E402

from app.main import app  # noqa: E402

VALID_HEADERS = {"X-Internal-Api-Key": "test-internal-key"}


@pytest.fixture(scope="session")
def client():
    # Контекстный менеджер запускает lifespan — без него модели не загрузятся.
    with TestClient(app) as test_client:
        yield test_client


@pytest.fixture
def auth_headers():
    return VALID_HEADERS
