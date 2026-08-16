"""Финансовый модуль."""

import pytest

BASE_REQUEST = {
    "history": [
        {"month": "2026-06", "income": 250000, "expense": 175000},
        {"month": "2026-07", "income": 250000, "expense": 180000},
    ],
    "categories": [
        {"category": "Еда", "amount": 60000},
        {"category": "Транспорт", "amount": 25000},
    ],
    "currency": "KZT",
}


def test_returns_forecast(client, auth_headers):
    response = client.post("/finance/analysis", headers=auth_headers, json=BASE_REQUEST)
    assert response.status_code == 200

    payload = response.json()
    assert payload["result"]["predicted_expense"] > 0
    assert payload["result"]["trend"] in {"rising", "stable", "falling"}


def test_response_always_carries_confidence(client, auth_headers):
    """Требование MASTER_GUIDE: ответ без оценки уверенности недопустим."""
    payload = client.post("/finance/analysis", headers=auth_headers, json=BASE_REQUEST).json()

    assert 0.0 <= payload["confidence"] <= 1.0
    assert isinstance(payload["is_confident"], bool)
    assert payload["explanation"]


def test_explainability_present(client, auth_headers):
    payload = client.post("/finance/analysis", headers=auth_headers, json=BASE_REQUEST).json()

    assert payload["contributions"], "Ответ должен объяснять, какие признаки повлияли"
    assert "feature" in payload["contributions"][0]


def test_top_category_detected(client, auth_headers):
    payload = client.post("/finance/analysis", headers=auth_headers, json=BASE_REQUEST).json()
    assert payload["result"]["top_category"] == "Еда"


def test_empty_history_rejected(client, auth_headers):
    """Pydantic обязан отсечь пустую историю до попадания в модель."""
    response = client.post("/finance/analysis", headers=auth_headers, json={"history": []})
    assert response.status_code == 422
