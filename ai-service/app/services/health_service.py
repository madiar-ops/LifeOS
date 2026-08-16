"""Инференс модели здоровья: прогноз настроения (sklearn) +
интегральная оценка самочувствия (PyTorch)."""

from __future__ import annotations

import numpy as np
import pandas as pd
from fastapi import HTTPException, status

from app.config import get_settings
from app.schemas.common import AIResponse, FeatureContribution
from app.schemas.health import HealthAnalysisRequest, HealthAssessment
from app.services.model_registry import registry

FEATURES = ["sleep_hours", "steps", "water_ml"]

FEATURE_LABELS = {
    "sleep_hours": "Сон, часов",
    "steps": "Шаги",
    "water_ml": "Вода, мл",
}

# Ориентиры взяты как общепринятые рекомендации ВОЗ по активности и сну.
# Это не медицинская диагностика — сервис лишь подсвечивает отклонения.
SLEEP_MIN = 7.0
STEPS_MIN = 6000
WATER_MIN = 1500


def analyze(request: HealthAnalysisRequest) -> AIResponse[HealthAssessment]:
    model = registry.get("health")

    if model is None:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Модель здоровья не обучена. Запустите app.ml.training.train_health.",
        )

    averages = _averages(request)

    features = pd.DataFrame([[averages["sleep_hours"], averages["steps"], averages["water_ml"]]],
                            columns=FEATURES)

    probabilities = model.predict_proba(features)[0]
    classes = model.classes_ if hasattr(model, "classes_") else model.named_steps["classifier"].classes_

    best_index = int(np.argmax(probabilities))
    predicted_mood = int(classes[best_index])

    # Уверенность = вероятность выбранного класса. Для многоклассовой
    # задачи это прямая и честная метрика: 0.9 значит, что модель
    # действительно уверена, 0.3 — что она колеблется.
    confidence = float(probabilities[best_index])

    wellbeing = _wellbeing_score(averages, predicted_mood)
    risks = _risk_factors(averages)

    assessment = HealthAssessment(
        wellbeing_score=round(wellbeing, 1),
        predicted_mood=predicted_mood,
        risk_factors=risks,
        recommendations=_recommendations(averages, risks),
    )

    settings = get_settings()

    return AIResponse[HealthAssessment](
        result=assessment,
        confidence=round(confidence, 4),
        is_confident=confidence >= settings.confidence_threshold,
        explanation=_explain(assessment, averages, confidence, settings.confidence_threshold, len(request.entries)),
        contributions=_contributions(averages),
        model_version=registry.get_version("health"),
    )


def _averages(request: HealthAnalysisRequest) -> dict[str, float]:
    """Усредняем по истории: одна плохая ночь не должна определять вывод."""
    sleep_values = [e.sleep_hours for e in request.entries if e.sleep_hours is not None]

    return {
        "sleep_hours": float(np.mean(sleep_values)) if sleep_values else 7.0,
        "steps": float(np.mean([e.steps for e in request.entries])),
        "water_ml": float(np.mean([e.water_ml for e in request.entries])),
    }


def _wellbeing_score(averages: dict[str, float], predicted_mood: int) -> float:
    """Интегральная оценка 0..100.

    Если PyTorch-модель загружена — считает она. Иначе используется
    линейное приближение по прогнозу настроения: сервис обязан отвечать,
    даже когда показательная модель не обучена.
    """
    bundle = registry.torch_bundle

    if bundle is None:
        return (predicted_mood - 1) / 4 * 100

    import torch

    values = np.array([[averages[name] for name in bundle["features"]]], dtype=np.float32)

    # Применяем ровно ту же нормализацию, что при обучении —
    # иначе модель получит данные в чужом масштабе.
    mean = np.array(bundle["mean"], dtype=np.float32)
    std = np.array(bundle["std"], dtype=np.float32)
    scaled = (values - mean) / std

    with torch.no_grad():
        prediction = bundle["model"](torch.tensor(scaled)).item()

    return float(np.clip(prediction, 0.0, 100.0))


def _risk_factors(averages: dict[str, float]) -> list[str]:
    risks: list[str] = []

    if averages["sleep_hours"] < SLEEP_MIN:
        risks.append(f"Недосып: в среднем {averages['sleep_hours']:.1f} ч вместо {SLEEP_MIN:.0f} ч")
    if averages["steps"] < STEPS_MIN:
        risks.append(f"Низкая активность: {averages['steps']:.0f} шагов в день")
    if averages["water_ml"] < WATER_MIN:
        risks.append(f"Мало воды: {averages['water_ml']:.0f} мл в день")

    return risks


def _recommendations(averages: dict[str, float], risks: list[str]) -> list[str]:
    if not risks:
        return ["Показатели в норме — сохраняйте текущий режим."]

    recommendations: list[str] = []

    if averages["sleep_hours"] < SLEEP_MIN:
        deficit = SLEEP_MIN - averages["sleep_hours"]
        recommendations.append(
            f"Добавьте примерно {deficit:.1f} ч сна — это сильнее всего влияет на самочувствие."
        )
    if averages["steps"] < STEPS_MIN:
        recommendations.append("Короткая прогулка 20–30 минут в день заметно поднимет активность.")
    if averages["water_ml"] < WATER_MIN:
        recommendations.append("Держите бутылку воды на рабочем месте — так проще добирать норму.")

    return recommendations


def _contributions(averages: dict[str, float]) -> list[FeatureContribution]:
    importances = registry.get_metadata("health").get("feature_importances", {})

    return [
        FeatureContribution(
            feature=FEATURE_LABELS.get(name, name),
            value=round(averages.get(name, 0.0), 2),
            impact=round(float(importance), 4),
        )
        for name, importance in sorted(importances.items(), key=lambda item: item[1], reverse=True)
    ]


def _explain(
    assessment: HealthAssessment,
    averages: dict[str, float],
    confidence: float,
    threshold: float,
    entry_count: int,
) -> str:
    parts: list[str] = []

    if confidence < threshold:
        parts.append(
            f"Уверенность прогноза невысокая ({confidence * 100:.0f}%) — "
            f"записей пока {entry_count}, выводы предварительные."
        )

    parts.append(f"Интегральная оценка самочувствия: {assessment.wellbeing_score:.0f} из 100.")
    parts.append(
        f"В среднем: сон {averages['sleep_hours']:.1f} ч, "
        f"{averages['steps']:.0f} шагов, {averages['water_ml']:.0f} мл воды в день."
    )

    if assessment.risk_factors:
        parts.append("Обратите внимание: " + "; ".join(assessment.risk_factors) + ".")
    else:
        parts.append("Отклонений от рекомендуемых значений не обнаружено.")

    return " ".join(parts)
