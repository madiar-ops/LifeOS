"""Валидация входных данных.

Граница между backend и AI-сервисом — не доверенная зона: ошибка в контракте
или подделанный запрос не должны превращаться в 500. Некорректные данные
обязаны отсекаться pydantic'ом до попадания в модель, иначе sklearn получит
мусор и упадёт уже внутри инференса.
"""

import pytest

from tests.conftest import AI_ENDPOINTS

INVALID_REQUESTS = [
    # --- Финансы ---
    pytest.param("/finance/analysis", {}, id="финансы: нет истории"),
    pytest.param("/finance/analysis", {"history": []}, id="финансы: пустая история"),
    pytest.param(
        "/finance/analysis",
        {"history": [{"month": "2026-07", "income": -1000, "expense": 500}]},
        id="финансы: отрицательный доход",
    ),
    pytest.param(
        "/finance/analysis",
        {"history": [{"month": "2026-07", "income": 1000, "expense": -500}]},
        id="финансы: отрицательный расход",
    ),
    pytest.param(
        "/finance/analysis",
        {"history": [{"month": "2026-07", "income": "много", "expense": 500}]},
        id="финансы: доход строкой",
    ),
    pytest.param(
        "/finance/analysis",
        {"history": [{"income": 1000, "expense": 500}]},
        id="финансы: month отсутствует",
    ),
    pytest.param(
        "/finance/analysis",
        {
            "history": [{"month": "2026-07", "income": 1000, "expense": 500}],
            "currency": "KAZAKHSTAN",
        },
        id="финансы: валюта длиннее трёх символов",
    ),
    pytest.param(
        "/finance/analysis",
        {
            "history": [{"month": "2026-07", "income": 1000, "expense": 500}],
            "categories": [{"category": "Еда", "amount": -1}],
        },
        id="финансы: отрицательная сумма категории",
    ),
    # --- Здоровье ---
    pytest.param("/health-analysis", {"entries": []}, id="здоровье: пустой список записей"),
    pytest.param(
        "/health-analysis",
        {"entries": [{"date": "2026-08-01", "mood": 7}]},
        id="здоровье: настроение вне шкалы 1..5",
    ),
    pytest.param(
        "/health-analysis",
        {"entries": [{"date": "2026-08-01", "sleep_hours": 30}]},
        id="здоровье: сон больше суток",
    ),
    pytest.param(
        "/health-analysis",
        {"entries": [{"date": "2026-08-01", "steps": -100}]},
        id="здоровье: отрицательные шаги",
    ),
    pytest.param(
        "/health-analysis",
        {"entries": [{"date": "2026-08-01", "weight": 0}]},
        id="здоровье: нулевой вес",
    ),
    pytest.param("/health-analysis", {"entries": {}}, id="здоровье: записи не списком"),
    # --- Учёба ---
    pytest.param("/study/summary", {"text": "Слишком коротко."}, id="конспект: текст короче 50"),
    pytest.param(
        "/study/summary",
        {"text": "а" * 200, "max_sentences": 2},
        id="конспект: меньше трёх предложений",
    ),
    pytest.param(
        "/study/summary",
        {"text": "а" * 200, "max_sentences": 21},
        id="конспект: больше двадцати предложений",
    ),
    pytest.param("/study/quiz", {"text": "а" * 200, "question_count": 0}, id="тест: ноль вопросов"),
    pytest.param(
        "/study/quiz",
        {"text": "а" * 200, "question_count": 16},
        id="тест: слишком много вопросов",
    ),
    pytest.param("/study/quiz", {"question_count": 5}, id="тест: нет материала"),
    # --- Карьера ---
    pytest.param(
        "/career/resume-analysis", {"resume_text": "Коротко"}, id="резюме: короче 50 символов"
    ),
    pytest.param(
        "/career/resume-analysis",
        {"resume_text": "О" * 100, "desired_position": "П" * 300},
        id="резюме: слишком длинная желаемая позиция",
    ),
    pytest.param(
        "/career/resume-analysis",
        {"resume_text": "О" * 100, "skills": "C#"},
        id="резюме: навыки не списком",
    ),
]


@pytest.mark.parametrize(("path", "body"), INVALID_REQUESTS)
def test_invalid_payload_gives_422(raw_client, auth_headers, path, body):
    """Некорректные данные — это 422 с разбором ошибки, а не 500.

    Клиент (ASP.NET Core) должен получить машиночитаемое указание, что именно
    он прислал не так, и не считать сервис сломанным.
    """
    response = raw_client.post(path, headers=auth_headers, json=body)

    assert response.status_code == 422, f"{path}: получен {response.status_code} — {response.text}"
    assert response.json()["detail"], "Ответ 422 обязан объяснять причину отказа"


@pytest.mark.parametrize(("path", "body"), INVALID_REQUESTS)
def test_invalid_payload_points_at_field(raw_client, auth_headers, path, body):
    """В ответе видно конкретное поле — иначе отладка интеграции превращается
    в угадывание."""
    detail = raw_client.post(path, headers=auth_headers, json=body).json()["detail"]

    assert all("loc" in error and "msg" in error for error in detail)


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_broken_json_is_not_a_crash(raw_client, auth_headers, path):
    """Битое тело запроса не должно ронять обработчик."""
    response = raw_client.post(
        path,
        headers={**auth_headers, "Content-Type": "application/json"},
        content="{не json".encode("utf-8"),
    )

    assert response.status_code == 422


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_unknown_fields_are_ignored(client, auth_headers, path):
    """Лишние поля не ломают запрос: backend может добавить своё поле
    раньше, чем AI-сервис научится его читать (совместимость версий)."""
    body = {**AI_ENDPOINTS[path], "неизвестное_поле": "значение", "user_id": 42}

    assert client.post(path, headers=auth_headers, json=body).status_code == 200
