"""Поведение сервиса в неполных условиях.

Дипломный стенд почти никогда не работает в идеальных условиях: у нового
пользователя две записи вместо года истории, ключ LLM не оплачен, модель
не обучена. Сервис обязан деградировать предсказуемо — с понятным кодом
ответа и честным объяснением, а не падать в 500.
"""

import pytest

from app.services import finance_service
from app.services.model_registry import registry
from tests.conftest import AI_ENDPOINTS

# Материал, состоящий из коротких тезисов, — так выглядит конспект лекции
# списком. Ни одно «предложение» не дотягивает до фильтра длины.
SHORT_PHRASES_TEXT = "Ядро. Память. Кэш. Регистр. Шина. Прерывание. Стек. Куча. Поток. Процесс."


# --- Модель не обучена --------------------------------------------------


@pytest.mark.parametrize(
    ("model_key", "path"),
    [
        pytest.param("finance", "/finance/analysis", id="финансовая модель"),
        pytest.param("health", "/health-analysis", id="модель здоровья"),
    ],
)
def test_missing_model_returns_503(raw_client, auth_headers, monkeypatch, model_key, path):
    """Отсутствие артефакта — 503, а не 500.

    Сервис жив и отвечает на /health, недоступна конкретная модель: клиент
    должен видеть временную недоступность, а не внутреннюю поломку.
    """
    monkeypatch.delitem(registry._models, model_key, raising=False)

    response = raw_client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path])

    assert response.status_code == 503
    assert "detail" in response.json()


def test_missing_model_message_tells_how_to_fix(raw_client, auth_headers, monkeypatch):
    """Сообщение об ошибке называет команду обучения — иначе разбираться
    придётся по исходникам."""
    monkeypatch.delitem(registry._models, "finance", raising=False)

    detail = raw_client.post(
        "/finance/analysis", headers=auth_headers, json=AI_ENDPOINTS["/finance/analysis"]
    ).json()["detail"]

    assert "train_finance" in detail


def test_health_endpoint_reports_missing_model(client, monkeypatch):
    """/health честно показывает, что модель не загружена — на это опирается
    проверка готовности контейнера."""
    monkeypatch.delitem(registry._models, "finance", raising=False)

    payload = client.get("/health").json()

    assert payload["models"]["finance"] is False
    assert payload["all_models_loaded"] is False


def test_wellbeing_falls_back_without_torch_model(client, auth_headers, monkeypatch):
    """Показательная нейросеть необязательна: без неё оценка самочувствия
    считается линейным приближением, а эндпоинт продолжает работать."""
    monkeypatch.setattr(registry, "_torch_bundle", None)

    payload = client.post(
        "/health-analysis", headers=auth_headers, json=AI_ENDPOINTS["/health-analysis"]
    ).json()

    assert 0 <= payload["result"]["wellbeing_score"] <= 100
    assert payload["confidence"] > 0


# --- Мало данных --------------------------------------------------------


def test_single_month_of_history_is_accepted(client, auth_headers):
    """Одного месяца истории достаточно для ответа: новый пользователь
    не должен упираться в отказ сервиса."""
    response = client.post(
        "/finance/analysis",
        headers=auth_headers,
        json={"history": [{"month": "2026-08", "income": 250000, "expense": 180000}]},
    )

    assert response.status_code == 200
    assert response.json()["result"]["predicted_expense"] > 0


def test_single_health_entry_is_accepted(client, auth_headers):
    """Одна запись — тоже данные. Прогноз будет грубым, но ответ обязан быть."""
    payload = client.post(
        "/health-analysis",
        headers=auth_headers,
        json={"entries": [{"date": "2026-08-01", "sleep_hours": 6.0, "steps": 3000, "water_ml": 1000}]},
    ).json()

    assert 1 <= payload["result"]["predicted_mood"] <= 5
    assert payload["result"]["risk_factors"], "Плохие показатели должны быть отмечены"


def test_entry_without_optional_fields_is_accepted(client, auth_headers):
    """Пользователь заполнил только дату — сервис не имеет права упасть
    на отсутствующих необязательных полях."""
    response = client.post(
        "/health-analysis", headers=auth_headers, json={"entries": [{"date": "2026-08-01"}]}
    )

    assert response.status_code == 200


def test_low_confidence_answer_says_so_in_text(client, auth_headers):
    """Если уверенность ниже порога, объяснение обязано предупредить об этом
    словами — пользователь читает текст, а не поле confidence."""
    payload = client.post(
        "/finance/analysis",
        headers=auth_headers,
        json={"history": [{"month": "2026-08", "income": 0, "expense": 0}]},
    ).json()

    assert payload["is_confident"] is False
    assert "ориентировочный" in payload["explanation"].lower()


# --- Работа без внешнего LLM -------------------------------------------


def test_llm_is_not_configured_in_tests(client):
    """Предпосылка следующих проверок: ключ LLM не задан. Если он появится
    в окружении, тесты запасных алгоритмов потеряют смысл — и мы это увидим."""
    assert client.get("/health").json()["llm_configured"] is False


def test_study_summary_works_without_llm(client, auth_headers):
    """Заявленный в проекте запасной путь Study: извлекающая суммаризация."""
    payload = client.post(
        "/study/summary", headers=auth_headers, json=AI_ENDPOINTS["/study/summary"]
    ).json()

    assert payload["result"]["source"] == "extractive"
    assert payload["result"]["summary"].strip()
    assert payload["confidence"] > 0


def test_career_analysis_works_without_llm(client, auth_headers):
    """Заявленный запасной путь Career: разбор по формальным признакам."""
    payload = client.post(
        "/career/resume-analysis",
        headers=auth_headers,
        json=AI_ENDPOINTS["/career/resume-analysis"],
    ).json()

    assert payload["result"]["source"] == "heuristic"
    assert 0 <= payload["result"]["overall_score"] <= 100
    assert payload["result"]["strengths"] or payload["result"]["weaknesses"]


def test_heuristic_confidence_is_below_llm_confidence(client, auth_headers):
    """Запасной алгоритм слабее — и это видно в ответе, а не только в коде."""
    career = client.post(
        "/career/resume-analysis",
        headers=auth_headers,
        json=AI_ENDPOINTS["/career/resume-analysis"],
    ).json()
    study = client.post(
        "/study/summary", headers=auth_headers, json=AI_ENDPOINTS["/study/summary"]
    ).json()

    assert career["confidence"] < 0.88
    assert study["confidence"] < 0.90


def test_quiz_refuses_honestly_without_llm(client, auth_headers):
    """Тест сгенерировать нечем — сервис отказывается прямо, но по-прежнему
    в общем конверте: нулевая уверенность и объяснение причины."""
    payload = client.post(
        "/study/quiz", headers=auth_headers, json=AI_ENDPOINTS["/study/quiz"]
    ).json()

    assert payload["result"]["questions"] == []
    assert payload["result"]["source"] == "unavailable"
    assert payload["confidence"] == 0.0
    assert payload["is_confident"] is False
    assert "LLM_API_KEY" in payload["explanation"]


# --- Текст, непригодный для суммаризации --------------------------------


def test_summary_of_short_phrases_is_not_empty(client, auth_headers):
    """Регрессия: материал из коротких фраз давал пустой конспект.

    Фильтр «предложение короче 25 символов — мусор» отбрасывал вообще всё,
    и пользователь получал 200 с пустым полем summary без единого намёка
    на причину.
    """
    payload = client.post(
        "/study/summary",
        headers=auth_headers,
        json={"text": SHORT_PHRASES_TEXT, "max_sentences": 3},
    ).json()

    assert payload["result"]["summary"].strip(), "Конспект не должен быть пустым"

    for point in payload["result"]["key_points"]:
        assert point in SHORT_PHRASES_TEXT, "Извлекающий метод не вправе выдумывать текст"


def test_unusable_material_gets_zero_confidence(client, auth_headers):
    """Если выделить не удалось ничего, ответ обязан признать это нулевой
    уверенностью — а не отдавать пустоту как обычный результат."""
    payload = client.post(
        "/study/summary", headers=auth_headers, json={"text": " " * 80}
    ).json()

    assert payload["result"]["summary"].strip() == ""
    assert payload["confidence"] == 0.0
    assert payload["is_confident"] is False
    assert payload["explanation"].strip()


def test_unexpected_failure_returns_structured_error(raw_client, auth_headers, monkeypatch):
    """Даже непредвиденный сбой уходит клиенту как JSON с кодом ошибки.

    Backend разбирает ответы машинно: HTML-страница трейсбека вместо JSON
    сломала бы обработку на стороне ASP.NET Core.
    """

    def boom(_request):
        raise RuntimeError("сбой инференса")

    monkeypatch.setattr(finance_service, "analyze", boom)

    response = raw_client.post(
        "/finance/analysis", headers=auth_headers, json=AI_ENDPOINTS["/finance/analysis"]
    )

    assert response.status_code == 500
    assert response.json()["code"] == "ai.internal_error"


def test_large_history_does_not_break_service(client, auth_headers):
    """Три года помесячной истории — верхняя граница реального объёма."""
    history = [
        {"month": f"{2024 + i // 12}-{i % 12 + 1:02d}", "income": 250000, "expense": 170000 + i * 500}
        for i in range(36)
    ]

    response = client.post("/finance/analysis", headers=auth_headers, json={"history": history})

    assert response.status_code == 200
    assert response.json()["result"]["trend"] in {"rising", "stable", "falling"}
