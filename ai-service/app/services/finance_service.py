"""Инференс финансовой модели."""

from __future__ import annotations

import numpy as np
import pandas as pd
from fastapi import HTTPException, status

from app.config import get_settings
from app.schemas.common import AIResponse, FeatureContribution
from app.schemas.finance import FinanceAnalysisRequest, FinanceForecast
from app.services.model_registry import registry

FEATURES = ["income", "prev_expense", "expense_ratio_prev", "month_of_year"]

FEATURE_LABELS = {
    "income": "Доход",
    "prev_expense": "Расход прошлого месяца",
    "expense_ratio_prev": "Доля трат от дохода",
    "month_of_year": "Месяц года",
}


def analyze(request: FinanceAnalysisRequest) -> AIResponse[FinanceForecast]:
    model = registry.get("finance")

    if model is None:
        # 503, а не 500: сервис жив, недоступна конкретная модель.
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Финансовая модель не обучена. Запустите app.ml.training.train_finance.",
        )

    latest = request.history[-1]
    month_number = _month_number(latest.month)

    income = max(latest.income, 1.0)
    prev_expense = latest.expense

    features = pd.DataFrame(
        [[income, prev_expense, prev_expense / income, month_number]],
        columns=FEATURES,
    )

    predicted_expense = float(model.predict(features)[0])
    predicted_expense = max(predicted_expense, 0.0)

    confidence = _confidence(predicted_expense)
    settings = get_settings()

    forecast = FinanceForecast(
        predicted_expense=round(predicted_expense, 2),
        predicted_balance=round(income - predicted_expense, 2),
        trend=_trend(request, predicted_expense),
        top_category=_top_category(request),
        savings_rate=round(max(0.0, (income - predicted_expense) / income), 4),
    )

    return AIResponse[FinanceForecast](
        result=forecast,
        confidence=round(confidence, 4),
        is_confident=confidence >= settings.confidence_threshold,
        explanation=_explain(forecast, request, confidence, settings.confidence_threshold),
        contributions=_contributions(features.iloc[0]),
        model_version=registry.get_version("finance"),
    )


def _month_number(month: str) -> int:
    """Из 'YYYY-MM' достаём номер месяца. Некорректный формат — не повод падать."""
    try:
        return int(month.split("-")[1])
    except (IndexError, ValueError):
        return 1


def _confidence(prediction: float) -> float:
    """Уверенность из типичной ошибки модели, зафиксированной при обучении.

    Чем больше разброс остатков относительно самого прогноза, тем меньше
    доверия. Это честнее, чем возвращать константу.
    """
    residual_std = registry.get_metadata("finance").get("residual_std")

    if not residual_std or prediction <= 0:
        return 0.5

    relative_error = float(residual_std) / prediction
    return float(np.clip(1.0 - relative_error, 0.05, 0.99))


def _trend(request: FinanceAnalysisRequest, predicted: float) -> str:
    """Направление тренда — по сравнению со средним за историю."""
    expenses = [item.expense for item in request.history]
    average = sum(expenses) / len(expenses)

    if average == 0:
        return "stable"

    change = (predicted - average) / average

    if change > 0.10:
        return "rising"
    if change < -0.10:
        return "falling"
    return "stable"


def _top_category(request: FinanceAnalysisRequest) -> str | None:
    if not request.categories:
        return None
    return max(request.categories, key=lambda c: c.amount).category


def _contributions(row: pd.Series) -> list[FeatureContribution]:
    """Вклад признаков берём из важностей, зафиксированных при обучении.

    Это глобальная важность, а не локальная (как в SHAP): для дипломного
    объёма достаточно, и работает мгновенно без лишних зависимостей.
    """
    importances = registry.get_metadata("finance").get("feature_importances", {})

    return [
        FeatureContribution(
            feature=FEATURE_LABELS.get(name, name),
            value=round(float(row[name]), 2),
            impact=round(float(importance), 4),
        )
        for name, importance in sorted(
            importances.items(), key=lambda item: item[1], reverse=True
        )
    ]


def _explain(
    forecast: FinanceForecast,
    request: FinanceAnalysisRequest,
    confidence: float,
    threshold: float,
) -> str:
    parts: list[str] = []

    if confidence < threshold:
        # Требование MASTER_GUIDE: при низкой уверенности AI говорит об этом первым делом.
        parts.append(
            "Данных пока мало, поэтому прогноз ориентировочный — "
            "точность вырастет после нескольких месяцев учёта."
        )

    trend_text = {
        "rising": "Расходы растут по сравнению со средним за период.",
        "falling": "Расходы снижаются — динамика положительная.",
        "stable": "Расходы держатся на стабильном уровне.",
    }
    parts.append(trend_text[forecast.trend])

    parts.append(
        f"Ожидаемые расходы в следующем месяце — около "
        f"{forecast.predicted_expense:,.0f} {request.currency}."
    )

    if forecast.predicted_balance < 0:
        parts.append("При текущем доходе прогноз уходит в минус — стоит пересмотреть траты.")
    elif forecast.savings_rate >= 0.20:
        parts.append(
            f"Получается откладывать около {forecast.savings_rate * 100:.0f}% дохода — это хороший запас."
        )

    if forecast.top_category:
        parts.append(f"Больше всего уходит на категорию «{forecast.top_category}».")

    return " ".join(parts)
