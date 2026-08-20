"""Ветка с внешней языковой моделью.

На стенде ключ LLM не задан, поэтому боевой путь Study и Career иначе не
проверялся бы вообще — а именно в нём живут разбор ответа модели и переход
на запасной алгоритм. Внешний вызов подменяется: тестируется поведение
сервиса, а не доступность чужого API.
"""

import pytest

from app.config import Settings
from app.services import career_service, study_service
from app.services.llm_client import LLMUnavailableError, _parse_json, complete_json

LLM_SETTINGS = Settings(INTERNAL_API_KEY="test-internal-key", LLM_API_KEY="fake-key-for-tests")


@pytest.fixture
def llm_enabled(monkeypatch):
    """Включаем режим «ключ LLM настроен» для обоих модулей сразу."""
    monkeypatch.setattr(study_service, "get_settings", lambda: LLM_SETTINGS)
    monkeypatch.setattr(career_service, "get_settings", lambda: LLM_SETTINGS)


def _answer(payload: dict):
    """Подмена вызова LLM: возвращает заранее заданный разобранный ответ."""

    async def fake_complete_json(*_args, **_kwargs):
        return payload

    return fake_complete_json


def _failure():
    async def fake_complete_json(*_args, **_kwargs):
        raise LLMUnavailableError("сервис недоступен")

    return fake_complete_json


# --- Успешный ответ модели ---------------------------------------------


def test_summary_from_llm_is_marked_and_confident(client, auth_headers, llm_enabled, monkeypatch):
    """Ответ от LLM помечается source=llm и получает более высокую уверенность,
    чем извлекающий метод: пользователь должен различать эти два случая."""
    monkeypatch.setattr(
        study_service,
        "complete_json",
        _answer({"summary": "Связный конспект материала.", "key_points": ["Первое", "Второе"]}),
    )

    payload = client.post(
        "/study/summary",
        headers=auth_headers,
        # Материал заведомо длиннее порога MIN_WORDS_FOR_QUALITY: иначе
        # уверенность снизится из-за объёма текста, а проверяем мы источник.
        json={"text": "Учебный материал про машинное обучение и его применение. " * 40},
    ).json()

    assert payload["result"]["source"] == "llm"
    assert payload["result"]["key_points"] == ["Первое", "Второе"]
    assert payload["confidence"] >= 0.9
    assert payload["is_confident"] is True


def test_quiz_from_llm_keeps_only_valid_questions(client, auth_headers, llm_enabled, monkeypatch):
    """Вопросы с битым правильным ответом или одним вариантом отбрасываются.

    Backend проверяет ответы студента по correct_index — вопрос с индексом
    вне диапазона сломал бы проверку и обнулил результат теста.
    """
    monkeypatch.setattr(
        study_service,
        "complete_json",
        _answer(
            {
                "questions": [
                    {"question": "Корректный?", "options": ["Да", "Нет"], "correct_index": 0},
                    {"question": "Индекс вне диапазона?", "options": ["Да", "Нет"], "correct_index": 5},
                    {"question": "Один вариант?", "options": ["Да"], "correct_index": 0},
                    {"question": "", "options": ["Да", "Нет"], "correct_index": 0},
                ]
            }
        ),
    )

    payload = client.post(
        "/study/quiz",
        headers=auth_headers,
        json={"text": "Материал для теста. " * 20, "question_count": 4},
    ).json()

    questions = payload["result"]["questions"]

    assert len(questions) == 1
    assert questions[0]["question"] == "Корректный?"
    assert payload["result"]["source"] == "llm"


def test_resume_score_from_llm_is_clamped(client, auth_headers, llm_enabled, monkeypatch):
    """Оценка приводится к шкале 0..100: модель может вернуть что угодно,
    а схема ответа обязана оставаться валидной."""
    monkeypatch.setattr(
        career_service,
        "complete_json",
        _answer({"overall_score": 5000, "strengths": ["Опыт"], "weaknesses": [], "suggestions": []}),
    )

    payload = client.post(
        "/career/resume-analysis",
        headers=auth_headers,
        json={"resume_text": "Опыт работы разработчиком три года. " * 5},
    ).json()

    assert payload["result"]["overall_score"] == 100
    assert payload["result"]["source"] == "llm"


# --- Отказ внешнего сервиса --------------------------------------------


def test_summary_falls_back_when_llm_fails(client, auth_headers, llm_enabled, monkeypatch):
    """Падение внешнего API не должно доходить до пользователя: включается
    локальная извлекающая суммаризация."""
    monkeypatch.setattr(study_service, "complete_json", _failure())

    text = "Первое предложение учебного материала. Второе предложение того же материала. " * 5

    payload = client.post(
        "/study/summary", headers=auth_headers, json={"text": text}
    ).json()

    assert payload["result"]["source"] == "extractive"
    assert payload["confidence"] < 0.9


def test_resume_falls_back_when_llm_fails(client, auth_headers, llm_enabled, monkeypatch):
    """То же для карьерного модуля — разбор по формальным признакам."""
    monkeypatch.setattr(career_service, "complete_json", _failure())

    payload = client.post(
        "/career/resume-analysis",
        headers=auth_headers,
        json={"resume_text": "Опыт работы разработчиком три года. " * 5},
    ).json()

    assert payload["result"]["source"] == "heuristic"
    assert payload["confidence"] < 0.88


def test_empty_llm_summary_falls_back(client, auth_headers, llm_enabled, monkeypatch):
    """Модель ответила формально корректно, но пусто — это не результат,
    и сервис доделывает работу локально."""
    monkeypatch.setattr(study_service, "complete_json", _answer({"summary": "", "key_points": []}))

    text = "Первое предложение учебного материала. Второе предложение того же материала. " * 5

    payload = client.post("/study/summary", headers=auth_headers, json={"text": text}).json()

    assert payload["result"]["source"] == "extractive"
    assert payload["result"]["summary"].strip()


def test_quiz_reports_zero_confidence_when_llm_fails(client, auth_headers, llm_enabled, monkeypatch):
    """У теста запасного пути нет — значит, честный отказ с нулевой
    уверенностью, но по-прежнему в общем конверте."""
    monkeypatch.setattr(study_service, "complete_json", _failure())

    payload = client.post(
        "/study/quiz", headers=auth_headers, json={"text": "Материал. " * 20, "question_count": 3}
    ).json()

    assert payload["result"]["questions"] == []
    assert payload["confidence"] == 0.0
    assert payload["is_confident"] is False


# --- Разбор ответа модели ----------------------------------------------


def test_markdown_fence_is_stripped():
    """LLM часто оборачивает JSON в ```json — без снятия ограждения разбор
    падал бы на каждом втором ответе."""
    assert _parse_json('```json\n{"summary": "текст"}\n```') == {"summary": "текст"}


def test_unstructured_answer_is_treated_as_unavailable():
    """Ответ не-JSON — это недоступность LLM, а не ошибка пользователя:
    вызывающий код обязан уйти на запасной алгоритм."""
    with pytest.raises(LLMUnavailableError):
        _parse_json("Конечно! Вот ваш конспект: ...")


@pytest.mark.asyncio
async def test_llm_call_without_key_fails_fast(monkeypatch):
    """Без ключа наружу не уходит ни одного запроса — ошибка поднимается
    до сетевого вызова."""
    monkeypatch.setattr(
        "app.services.llm_client.get_settings", lambda: Settings(LLM_API_KEY="")
    )

    with pytest.raises(LLMUnavailableError):
        await complete_json("system", "user")
