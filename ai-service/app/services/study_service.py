"""Учебный модуль: конспект и тесты по материалу."""

from __future__ import annotations

import logging

from app.config import get_settings
from app.schemas.common import AIResponse
from app.schemas.study import (
    QuizQuestion,
    QuizRequest,
    QuizResult,
    StudySummary,
    StudySummaryRequest,
)
from app.services.llm_client import LLMUnavailableError, complete_json
from app.utils.text_utils import extractive_summary, keywords, word_count

logger = logging.getLogger(__name__)

# Уверенность различается по способу получения результата — это не
# косметика: извлекающий алгоритм заведомо слабее LLM, и пользователь
# должен видеть разницу.
LLM_CONFIDENCE = 0.90
EXTRACTIVE_CONFIDENCE = 0.62
MIN_WORDS_FOR_QUALITY = 150

SUMMARY_SYSTEM_PROMPT = (
    "Ты помогаешь студенту законспектировать учебный материал. "
    "Опирайся ТОЛЬКО на предоставленный текст, ничего не добавляй от себя. "
    "Ответь строго JSON без markdown: "
    '{"summary": "связный конспект", "key_points": ["пункт", "пункт"]}'
)

QUIZ_SYSTEM_PROMPT = (
    "Ты составляешь проверочный тест по учебному материалу. "
    "Вопросы должны проверять понимание текста, а не общие знания. "
    "Все варианты ответов правдоподобны, правильный ровно один. "
    "Ответь строго JSON без markdown: "
    '{"questions": [{"question": "...", "options": ["A","B","C","D"], '
    '"correct_index": 0, "explanation": "почему"}]}'
)


async def summarize(request: StudySummaryRequest) -> AIResponse[StudySummary]:
    settings = get_settings()
    words = word_count(request.text)

    if settings.llm_enabled:
        try:
            data = await complete_json(
                SUMMARY_SYSTEM_PROMPT,
                f"Язык ответа: {request.language}. "
                f"Не более {request.max_sentences} предложений.\n\nТекст:\n{request.text[:15000]}",
            )

            summary = StudySummary(
                summary=str(data.get("summary", "")).strip(),
                key_points=[str(p) for p in data.get("key_points", [])][:10],
                source="llm",
            )

            if summary.summary:
                return _build(summary, LLM_CONFIDENCE, words, settings.confidence_threshold)

        except LLMUnavailableError:
            # Молча переключаемся на локальный путь — для пользователя
            # важен результат, а не то, каким способом он получен.
            logger.info("LLM недоступен, используется извлекающая суммаризация.")

    text, sentences = extractive_summary(request.text, request.max_sentences)

    summary = StudySummary(
        summary=text,
        key_points=sentences[:5],
        source="extractive",
    )

    return _build(summary, EXTRACTIVE_CONFIDENCE, words, settings.confidence_threshold)


async def generate_quiz(request: QuizRequest) -> AIResponse[QuizResult]:
    settings = get_settings()

    if not settings.llm_enabled:
        # Здесь запасного пути нет: сгенерировать осмысленные варианты
        # ответов статистическим методом невозможно. Честнее сказать прямо,
        # чем выдать бессмысленный тест.
        return AIResponse[QuizResult](
            result=QuizResult(questions=[], source="unavailable"),
            confidence=0.0,
            is_confident=False,
            explanation=(
                "Генерация тестов требует внешней языковой модели, "
                "а ключ LLM_API_KEY не настроен."
            ),
            model_version="quiz-llm-1.0",
        )

    try:
        data = await complete_json(
            QUIZ_SYSTEM_PROMPT,
            f"Язык: {request.language}. Составь ровно {request.question_count} вопросов.\n\n"
            f"Материал:\n{request.text[:15000]}",
            max_tokens=3000,
        )

        questions = [
            QuizQuestion(
                question=str(item.get("question", "")).strip(),
                options=[str(o) for o in item.get("options", [])],
                correct_index=int(item.get("correct_index", 0)),
                explanation=str(item.get("explanation", "")),
            )
            for item in data.get("questions", [])
            if item.get("question") and len(item.get("options", [])) >= 2
        ]

        # Отбрасываем вопросы, где индекс правильного ответа вне диапазона:
        # такой вопрос сломал бы проверку на стороне backend.
        questions = [q for q in questions if 0 <= q.correct_index < len(q.options)]

    except LLMUnavailableError as exc:
        logger.warning("Генерация теста не удалась: %s", exc)
        questions = []

    confidence = LLM_CONFIDENCE if questions else 0.0

    return AIResponse[QuizResult](
        result=QuizResult(questions=questions, source="llm" if questions else "unavailable"),
        confidence=confidence,
        is_confident=confidence >= settings.confidence_threshold,
        explanation=(
            f"Сгенерировано вопросов: {len(questions)}."
            if questions
            else "Не удалось составить тест по этому материалу."
        ),
        model_version="quiz-llm-1.0",
    )


def _build(
    summary: StudySummary, base_confidence: float, words: int, threshold: float
) -> AIResponse[StudySummary]:
    # Пустой конспект — это не результат. Отдать его с обычной уверенностью
    # значило бы нарушить требование MASTER_GUIDE «если AI не уверен, он
    # сообщает об этом»: пользователь увидел бы пустое поле без объяснения.
    if not summary.summary.strip():
        return AIResponse[StudySummary](
            result=summary,
            confidence=0.0,
            is_confident=False,
            explanation=(
                "Из присланного материала не удалось выделить ни одного предложения. "
                "Проверьте, что текст извлёкся из файла корректно."
            ),
            model_version="study-1.0",
        )

    confidence = base_confidence

    # Короткий текст — мало опоры для конспекта, снижаем уверенность.
    if words < MIN_WORDS_FOR_QUALITY:
        confidence *= 0.75

    explanation_parts: list[str] = []

    if summary.source == "extractive":
        explanation_parts.append(
            "Конспект составлен извлекающим методом: выбраны наиболее "
            "информативные предложения исходного текста без переформулирования."
        )
    else:
        explanation_parts.append("Конспект составлен языковой моделью на основе исходного текста.")

    if words < MIN_WORDS_FOR_QUALITY:
        explanation_parts.append(
            f"Материал короткий ({words} слов) — конспект может быть неполным."
        )

    return AIResponse[StudySummary](
        result=summary,
        confidence=round(confidence, 4),
        is_confident=confidence >= threshold,
        explanation=" ".join(explanation_parts),
        model_version="study-1.0",
    )
