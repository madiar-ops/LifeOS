"""Проверка внутреннего канала.

Это самый важный тест сервиса: если ключ перестанет работать,
AI-сервис окажется открыт всему интернету.
"""

import pytest
from fastapi import HTTPException
from fastapi.routing import APIRoute

from app.config import Settings
from app.main import app
from app.security import verify_internal_api_key
from tests.conftest import AI_ENDPOINTS, VALID_HEADERS

PUBLIC_PATHS = {"/ping", "/health"}

VALID_KEY = VALID_HEADERS["X-Internal-Api-Key"]


def test_ping_is_public(client):
    """Служебные эндпоинты должны отвечать без ключа — иначе
    оркестратор не сможет проверить живость контейнера."""
    response = client.get("/ping")
    assert response.status_code == 200
    assert response.json()["status"] == "healthy"


def test_health_reports_models(client):
    payload = client.get("/health").json()
    assert "models" in payload
    assert set(payload["models"]) == {"finance", "health", "wellbeing_torch"}


def test_request_without_key_rejected(client):
    response = client.post(
        "/finance/analysis",
        json={"history": [{"month": "2026-07", "income": 250000, "expense": 180000}]},
    )
    assert response.status_code == 401


def test_request_with_wrong_key_rejected(client):
    response = client.post(
        "/finance/analysis",
        headers={"X-Internal-Api-Key": "definitely-wrong"},
        json={"history": [{"month": "2026-07", "income": 250000, "expense": 180000}]},
    )
    assert response.status_code == 401


# --- Полнота защиты -----------------------------------------------------


def test_every_protected_route_requires_key(client):
    """Обход всех маршрутов приложения.

    Точечные тесты по одному эндпоинту не спасают от главной ошибки: забыть
    зависимость на новом роутере. Здесь маршруты берутся из самого приложения,
    поэтому незащищённый эндпоинт нельзя добавить незаметно.
    """
    unprotected: list[str] = []

    for route in app.routes:
        if not isinstance(route, APIRoute) or route.path in PUBLIC_PATHS:
            continue

        response = client.request(next(iter(route.methods)), route.path, json={})

        if response.status_code not in (401, 403):
            unprotected.append(f"{route.path} -> {response.status_code}")

    assert not unprotected, f"Эндпоинты отвечают без ключа: {unprotected}"


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_valid_body_does_not_bypass_key(client, path):
    """Корректное тело запроса не должно быть «пропуском»: проверка ключа
    обязана срабатывать раньше, чем сервис вообще посмотрит на данные."""
    response = client.post(path, json=AI_ENDPOINTS[path])
    assert response.status_code == 401


# --- Устойчивость самой проверки ключа ----------------------------------


def test_non_ascii_key_is_rejected_not_crashed(raw_client):
    """Регрессия: ключ с нелатинскими символами давал 500 вместо 401.

    hmac.compare_digest отказывается сравнивать строки вне ASCII и бросал
    TypeError — посторонний запрос вызывал внутреннюю ошибку сервиса
    (и запись в лог) вместо тихого отказа в доступе.
    """
    response = raw_client.post(
        "/finance/analysis",
        headers={"X-Internal-Api-Key": "ключ-кириллицей".encode("utf-8")},
        json=AI_ENDPOINTS["/finance/analysis"],
    )

    assert response.status_code == 401, response.text


@pytest.mark.parametrize(
    "key",
    [
        pytest.param("", id="пустая строка"),
        pytest.param("   ", id="пробелы"),
        pytest.param(VALID_KEY[:-1], id="ключ без последнего символа"),
        pytest.param(VALID_KEY + "x", id="ключ с лишним символом"),
        pytest.param(VALID_KEY.upper(), id="другой регистр"),
        pytest.param(f" {VALID_KEY} ", id="ключ в пробелах"),
    ],
)
def test_almost_correct_keys_rejected(raw_client, key):
    """Сравнение строгое: ни префикс, ни другой регистр, ни лишние пробелы
    не должны приниматься за верный ключ."""
    response = raw_client.post(
        "/finance/analysis",
        headers={"X-Internal-Api-Key": key},
        json=AI_ENDPOINTS["/finance/analysis"],
    )

    assert response.status_code == 401


def test_rejection_does_not_leak_expected_key(client):
    """Ответ об отказе не должен содержать сам секрет — иначе 401
    превращается в способ его узнать."""
    response = client.post(
        "/finance/analysis",
        headers={"X-Internal-Api-Key": "wrong"},
        json=AI_ENDPOINTS["/finance/analysis"],
    )

    assert VALID_KEY not in response.text


def test_public_endpoints_do_not_leak_configuration(client):
    """/health показывает состояние моделей, но не секреты."""
    body = client.get("/health").text + client.get("/ping").text

    assert VALID_KEY not in body


def test_header_name_is_case_insensitive(client):
    """HTTP-заголовки регистронезависимы: backend не обязан писать имя
    ровно так же, как оно объявлено в коде."""
    response = client.post(
        "/finance/analysis",
        headers={"x-internal-api-key": VALID_KEY},
        json=AI_ENDPOINTS["/finance/analysis"],
    )

    assert response.status_code == 200


@pytest.mark.asyncio
async def test_service_without_configured_key_refuses_to_serve():
    """Сервис без настроенного INTERNAL_API_KEY обязан отказывать всем.

    Принимать запросы «раз ключ не задан» означало бы открытый AI-сервис
    в проде из-за одной забытой переменной окружения.
    """
    settings = Settings(INTERNAL_API_KEY="")

    with pytest.raises(HTTPException) as error:
        await verify_internal_api_key(x_internal_api_key="что угодно", settings=settings)

    assert error.value.status_code == 500
