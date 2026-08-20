"""Единый конверт AIResponse.

Главное требование MASTER_GUIDE к AI-части: «AI никогда не генерирует
случайные ответы. Если AI не уверен — он сообщает об этом». Технически это
значит, что НИ ОДИН эндпоинт не имеет права вернуть результат без оценки
уверенности. Проверяется здесь двумя способами сразу: по фактическим ответам
и по схеме OpenAPI — чтобы новый эндпоинт нельзя было добавить в обход конверта.
"""

import pytest

from app.main import app
from tests.conftest import AI_ENDPOINTS

ENVELOPE_FIELDS = {
    "result",
    "confidence",
    "is_confident",
    "explanation",
    "contributions",
    "model_version",
}

PUBLIC_PATHS = {"/ping", "/health"}


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_envelope_fields_present(client, auth_headers, path):
    """Ответ любого AI-эндпоинта содержит полный конверт целиком."""
    response = client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path])

    assert response.status_code == 200, response.text
    assert ENVELOPE_FIELDS <= set(response.json()), f"{path}: конверт неполный"


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_confidence_is_valid_probability(client, auth_headers, path):
    """confidence — вероятность, а не произвольное число: 0..1 и не None."""
    payload = client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path]).json()

    confidence = payload["confidence"]

    assert isinstance(confidence, (int, float)) and not isinstance(confidence, bool)
    assert 0.0 <= confidence <= 1.0, f"{path}: confidence вне диапазона"


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_is_confident_matches_threshold(client, auth_headers, path, confidence_threshold):
    """is_confident не декоративный флаг: он обязан следовать из confidence.

    Расхождение означало бы, что пользователю показывают «уверенный» ответ
    там, где модель на самом деле колеблется.
    """
    payload = client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path]).json()

    assert isinstance(payload["is_confident"], bool)
    assert payload["is_confident"] is (payload["confidence"] >= confidence_threshold)


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_explanation_is_human_readable(client, auth_headers, path):
    """Объяснение обязано быть непустым текстом — модель не чёрный ящик."""
    payload = client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path]).json()

    explanation = payload["explanation"]

    assert isinstance(explanation, str)
    assert len(explanation.strip()) >= 20, f"{path}: объяснение бессодержательно"


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_model_version_is_reported(client, auth_headers, path):
    """Версия модели нужна для разбора инцидентов: по логу ответа должно быть
    видно, какой именно артефакт выдал этот прогноз."""
    payload = client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path]).json()

    version = payload["model_version"]

    assert isinstance(version, str) and version.strip()
    assert version != "unknown", f"{path}: версия модели потеряна"


@pytest.mark.parametrize("path", sorted(AI_ENDPOINTS))
def test_contributions_are_well_formed(client, auth_headers, path):
    """contributions может быть пустым (текстовые модули вкладов не дают),
    но если он есть — структура строго фиксирована, backend на неё завязан."""
    payload = client.post(path, headers=auth_headers, json=AI_ENDPOINTS[path]).json()

    assert isinstance(payload["contributions"], list)

    for contribution in payload["contributions"]:
        assert set(contribution) == {"feature", "value", "impact"}
        assert isinstance(contribution["feature"], str) and contribution["feature"]


def test_openapi_declares_confidence_for_every_ai_endpoint():
    """Структурная гарантия: конверт зафиксирован в схеме, а не только
    в текущей реализации. Эндпоинт, отдающий голый результат без confidence,
    завалит этот тест ещё до того, как его кто-то вызовет.
    """
    schema = app.openapi()
    components = schema["components"]["schemas"]

    checked = 0

    for path, methods in schema["paths"].items():
        if path in PUBLIC_PATHS:
            continue

        for operation in methods.values():
            ref = operation["responses"]["200"]["content"]["application/json"]["schema"]["$ref"]
            model = components[ref.rsplit("/", 1)[-1]]

            required = set(model.get("required", []))

            assert {"confidence", "is_confident", "explanation"} <= required, (
                f"{path}: схема ответа допускает результат без оценки уверенности"
            )
            checked += 1

    assert checked == len(AI_ENDPOINTS), "Проверены не все защищённые эндпоинты"
