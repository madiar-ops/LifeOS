"""Клиент внешнего LLM для задач, где статистическая модель бессильна.

Суммаризация текста и разбор резюме требуют понимания языка — обучать
такую модель в рамках диплома нереально, и честнее взять готовый LLM API.

Ключ необязателен: без него модули работают на локальных алгоритмах
(см. text_utils). Проект должен запускаться и демонстрироваться
без внешних платных сервисов.
"""

from __future__ import annotations

import json
import logging

import httpx

from app.config import get_settings

logger = logging.getLogger(__name__)

ANTHROPIC_URL = "https://api.anthropic.com/v1/messages"
ANTHROPIC_VERSION = "2023-06-01"


class LLMUnavailableError(RuntimeError):
    """LLM не сконфигурирован или недоступен — вызывающий код обязан
    переключиться на локальный алгоритм, а не возвращать ошибку пользователю."""


async def complete_json(system_prompt: str, user_prompt: str, max_tokens: int = 2000) -> dict:
    """Запрос к LLM с ожиданием строгого JSON в ответе."""

    settings = get_settings()

    if not settings.llm_enabled:
        raise LLMUnavailableError("LLM_API_KEY не задан.")

    payload = {
        "model": settings.llm_model,
        "max_tokens": max_tokens,
        "system": system_prompt,
        "messages": [{"role": "user", "content": user_prompt}],
    }

    headers = {
        "x-api-key": settings.llm_api_key,
        "anthropic-version": ANTHROPIC_VERSION,
        "content-type": "application/json",
    }

    try:
        async with httpx.AsyncClient(timeout=settings.llm_timeout_seconds) as client:
            response = await client.post(ANTHROPIC_URL, json=payload, headers=headers)
            response.raise_for_status()
            data = response.json()
    except httpx.HTTPError as exc:
        # Падение внешнего сервиса не должно ронять наш эндпоинт:
        # выше по стеку сработает локальный запасной алгоритм.
        logger.warning("Обращение к LLM не удалось: %s", exc)
        raise LLMUnavailableError(str(exc)) from exc

    text = "".join(
        block.get("text", "") for block in data.get("content", []) if block.get("type") == "text"
    ).strip()

    return _parse_json(text)


def _parse_json(text: str) -> dict:
    """LLM иногда оборачивает JSON в markdown-ограждение — снимаем его."""

    cleaned = text.strip()

    if cleaned.startswith("```"):
        lines = [line for line in cleaned.splitlines() if not line.strip().startswith("```")]
        cleaned = "\n".join(lines).strip()

    try:
        return json.loads(cleaned)
    except json.JSONDecodeError as exc:
        logger.warning("LLM вернул невалидный JSON: %s", cleaned[:200])
        raise LLMUnavailableError("LLM вернул неструктурированный ответ.") from exc
