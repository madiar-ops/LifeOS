"""Проверка внутреннего канала.

Это самый важный тест сервиса: если ключ перестанет работать,
AI-сервис окажется открыт всему интернету.
"""


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
