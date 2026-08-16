"""Карьерный модуль: анализ резюме."""

from __future__ import annotations

import logging

from app.config import get_settings
from app.schemas.career import ResumeAnalysis, ResumeAnalysisRequest
from app.schemas.common import AIResponse
from app.services.llm_client import LLMUnavailableError, complete_json
from app.utils.text_utils import keywords, word_count

logger = logging.getLogger(__name__)

LLM_CONFIDENCE = 0.88
HEURISTIC_CONFIDENCE = 0.55

SYSTEM_PROMPT = (
    "Ты карьерный консультант. Разбери резюме честно и конкретно: "
    "никаких общих слов вроде «улучшите формулировки». "
    "Опирайся только на текст резюме. "
    "Ответь строго JSON без markdown: "
    '{"overall_score": 0-100, "strengths": [], "weaknesses": [], '
    '"missing_skills": [], "suggestions": []}'
)

# Признаки сильного резюме — проверяются в запасном режиме без LLM.
QUANTIFIER_HINTS = ["%", "раз", "млн", "тыс", "год", "лет", "проект", "команд"]
SECTION_HINTS = ["опыт", "образован", "навык", "проект", "experience", "education", "skills"]


async def analyze(request: ResumeAnalysisRequest) -> AIResponse[ResumeAnalysis]:
    settings = get_settings()

    if settings.llm_enabled:
        try:
            data = await complete_json(
                SYSTEM_PROMPT,
                f"Язык ответа: {request.language}.\n"
                f"Желаемая позиция: {request.desired_position or 'не указана'}.\n"
                f"Заявленные навыки: {', '.join(request.skills) or 'не указаны'}.\n\n"
                f"Резюме:\n{request.resume_text[:15000]}",
            )

            analysis = ResumeAnalysis(
                overall_score=float(max(0.0, min(100.0, data.get("overall_score", 50)))),
                strengths=[str(s) for s in data.get("strengths", [])][:8],
                weaknesses=[str(s) for s in data.get("weaknesses", [])][:8],
                missing_skills=[str(s) for s in data.get("missing_skills", [])][:10],
                suggestions=[str(s) for s in data.get("suggestions", [])][:8],
                source="llm",
            )

            return AIResponse[ResumeAnalysis](
                result=analysis,
                confidence=LLM_CONFIDENCE,
                is_confident=LLM_CONFIDENCE >= settings.confidence_threshold,
                explanation=(
                    "Резюме разобрано языковой моделью с учётом желаемой позиции. "
                    "Рекомендации носят совещательный характер — решение за вами."
                ),
                model_version="career-llm-1.0",
            )

        except LLMUnavailableError:
            logger.info("LLM недоступен, применяется эвристический разбор резюме.")

    return _heuristic(request, settings.confidence_threshold)


def _heuristic(request: ResumeAnalysisRequest, threshold: float) -> AIResponse[ResumeAnalysis]:
    """Запасной разбор по формальным признакам.

    Он не понимает смысл — только проверяет структуру и объём. Поэтому
    уверенность заметно ниже, и это прямо сказано пользователю.
    """

    text = request.resume_text
    lower = text.lower()
    words = word_count(text)

    strengths: list[str] = []
    weaknesses: list[str] = []
    suggestions: list[str] = []

    score = 50.0

    # Объём
    if words < 120:
        weaknesses.append("Резюме слишком короткое — работодателю не за что зацепиться.")
        suggestions.append("Раскройте опыт: задачи, инструменты, результат.")
        score -= 15
    elif words > 900:
        weaknesses.append("Резюме перегружено — ключевое теряется в объёме.")
        suggestions.append("Сократите до 1–2 страниц, оставьте релевантное позиции.")
        score -= 8
    else:
        strengths.append("Объём резюме соответствует ожиданиям рекрутера.")
        score += 10

    # Структура
    present_sections = [hint for hint in SECTION_HINTS if hint in lower]
    if len(present_sections) >= 3:
        strengths.append("Присутствуют основные разделы: опыт, образование, навыки.")
        score += 10
    else:
        weaknesses.append("Не хватает базовых разделов резюме.")
        suggestions.append("Добавьте разделы «Опыт», «Образование», «Навыки».")
        score -= 10

    # Измеримые результаты
    if any(hint in lower for hint in QUANTIFIER_HINTS):
        strengths.append("Есть конкретика: сроки, объёмы или измеримые результаты.")
        score += 12
    else:
        weaknesses.append("Достижения не измеримы — только перечисление обязанностей.")
        suggestions.append("Замените «занимался разработкой» на «сократил время отклика на 40%».")
        score -= 12

    # Соответствие заявленным навыкам
    missing = [skill for skill in request.skills if skill.lower() not in lower]
    if missing:
        suggestions.append(
            f"Навыки не отражены в тексте резюме: {', '.join(missing[:5])}."
        )
        score -= min(len(missing) * 3, 12)

    # Упоминание желаемой позиции
    if request.desired_position and request.desired_position.lower() not in lower:
        weaknesses.append("Желаемая позиция не отражена в резюме.")
        suggestions.append(
            f"Добавьте заголовок с позицией «{request.desired_position}» в начало."
        )
        score -= 8

    analysis = ResumeAnalysis(
        overall_score=round(max(0.0, min(100.0, score)), 1),
        strengths=strengths,
        weaknesses=weaknesses,
        missing_skills=missing[:10],
        suggestions=suggestions,
        source="heuristic",
    )

    return AIResponse[ResumeAnalysis](
        result=analysis,
        confidence=HEURISTIC_CONFIDENCE,
        is_confident=HEURISTIC_CONFIDENCE >= threshold,
        explanation=(
            "Разбор выполнен по формальным признакам: объём, структура, наличие "
            "измеримых результатов. Смысловое содержание не оценивалось — "
            "для полноценного анализа нужен ключ внешней языковой модели."
        ),
        model_version="career-heuristic-1.0",
    )
