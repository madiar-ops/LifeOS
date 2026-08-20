"""Объяснимость прогнозов.

Требование к AI-части проекта: модель не чёрный ящик. Пользователь должен
видеть, какие показатели повлияли на результат, а не только сам результат.
Технически это поле contributions — и оно проверяется здесь по существу,
а не на факт наличия.
"""

import pytest

from app.services.model_registry import registry
from tests.conftest import VALID_HEADERS

FINANCE_REQUEST = {
    "history": [
        {"month": "2026-06", "income": 250000, "expense": 175000},
        {"month": "2026-07", "income": 240000, "expense": 182000},
    ],
    "categories": [{"category": "Еда", "amount": 60000}],
}

HEALTH_REQUEST = {
    "entries": [
        {"date": "2026-08-01", "sleep_hours": 8.0, "steps": 10000, "water_ml": 2000, "mood": 4},
        {"date": "2026-08-02", "sleep_hours": 6.0, "steps": 8000, "water_ml": 1000, "mood": 4},
    ]
}


@pytest.fixture(scope="module")
def finance_payload(client):
    return client.post("/finance/analysis", headers=VALID_HEADERS, json=FINANCE_REQUEST).json()


@pytest.fixture(scope="module")
def health_payload(client):
    return client.post("/health-analysis", headers=VALID_HEADERS, json=HEALTH_REQUEST).json()


@pytest.mark.parametrize("model_key", ["finance", "health"])
def test_every_model_feature_is_explained(request, model_key):
    """Объяснение покрывает все признаки модели.

    Умолчать про часть признаков хуже, чем не объяснять вовсе: пользователь
    решит, что учтено только показанное.
    """
    payload = request.getfixturevalue(f"{model_key}_payload")
    expected_count = len(registry.get_metadata(model_key)["feature_importances"])

    assert len(payload["contributions"]) == expected_count


@pytest.mark.parametrize("model_key", ["finance", "health"])
def test_contributions_sorted_by_impact(request, model_key):
    """Сначала то, что повлияло сильнее всего — интерфейс показывает список
    как есть и не пересортировывает."""
    payload = request.getfixturevalue(f"{model_key}_payload")
    impacts = [item["impact"] for item in payload["contributions"]]

    assert impacts == sorted(impacts, reverse=True)


@pytest.mark.parametrize("model_key", ["finance", "health"])
def test_impacts_are_shares_of_one(request, model_key):
    """Вклады — доли важности признаков, поэтому неотрицательны и в сумме
    дают единицу с точностью до округления."""
    payload = request.getfixturevalue(f"{model_key}_payload")
    impacts = [item["impact"] for item in payload["contributions"]]

    assert all(impact >= 0 for impact in impacts)
    assert abs(sum(impacts) - 1.0) < 0.01


@pytest.mark.parametrize("model_key", ["finance", "health"])
def test_feature_names_are_human_readable(request, model_key):
    """В ответ уходят понятные подписи, а не имена колонок датасета:
    поле показывается пользователю без дополнительного перевода."""
    payload = request.getfixturevalue(f"{model_key}_payload")
    names = {item["feature"] for item in payload["contributions"]}

    assert not (names & set(registry.get_metadata(model_key)["feature_importances"]))
    assert all(any("а" <= symbol.lower() <= "я" for symbol in name) for name in names)


def test_finance_contribution_values_come_from_request(finance_payload):
    """Значения признаков — реальные данные пользователя, а не константы
    из обучающей выборки. Иначе объяснение относилось бы к чужому случаю."""
    values = {item["feature"]: item["value"] for item in finance_payload["contributions"]}

    assert values["Расход прошлого месяца"] == pytest.approx(182000.0)
    assert values["Доход"] == pytest.approx(240000.0)
    assert values["Месяц года"] == pytest.approx(7.0)


def test_health_contribution_values_are_period_averages(health_payload):
    """Модель работает по средним за период — объяснение обязано показывать
    те же средние, иначе цифры в ответе противоречат друг другу."""
    values = {item["feature"]: item["value"] for item in health_payload["contributions"]}

    assert values["Сон, часов"] == pytest.approx(7.0)
    assert values["Шаги"] == pytest.approx(9000.0)
    assert values["Вода, мл"] == pytest.approx(1500.0)


def test_explanation_mentions_key_numbers(health_payload):
    """Текстовое объяснение и числовой результат — про один и тот же расчёт."""
    explanation = health_payload["explanation"]
    score = health_payload["result"]["wellbeing_score"]

    assert f"{score:.0f}" in explanation
    assert "сон" in explanation.lower()


def test_finance_explanation_matches_trend(finance_payload):
    """Направление тренда в тексте соответствует полю trend."""
    expected_word = {
        "rising": "растут",
        "falling": "снижаются",
        "stable": "стабильном",
    }[finance_payload["result"]["trend"]]

    assert expected_word in finance_payload["explanation"]


@pytest.mark.parametrize("path", ["/study/summary", "/career/resume-analysis"])
def test_text_modules_explain_in_words(client, auth_headers, path):
    """У текстовых модулей нет числовых вкладов — там объяснимость это текст:
    каким способом получен результат и чего от него ждать."""
    bodies = {
        "/study/summary": {"text": "Первое предложение материала о предмете. " * 5},
        "/career/resume-analysis": {"resume_text": "Опыт работы разработчиком три года. " * 5},
    }

    payload = client.post(path, headers=auth_headers, json=bodies[path]).json()

    assert payload["contributions"] == []
    assert len(payload["explanation"]) > 40
