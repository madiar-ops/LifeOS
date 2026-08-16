"""Общие схемы ответов AI-сервиса."""

from typing import Generic, TypeVar

from pydantic import BaseModel, Field

T = TypeVar("T")


class FeatureContribution(BaseModel):
    """Вклад одного признака в предсказание — основа объяснимости.

    Модель не должна быть чёрным ящиком: пользователь видит, ПОЧЕМУ
    получил такой прогноз, а не только сам прогноз.
    """

    feature: str = Field(description="Понятное название признака")
    value: float = Field(description="Значение признака у пользователя")
    impact: float = Field(description="Вклад в результат: положительный увеличивает, отрицательный уменьшает")


class AIResponse(BaseModel, Generic[T]):
    """Единая обёртка любого ответа модели.

    confidence и is_confident обязательны везде: требование MASTER_GUIDE —
    AI не выдаёт случайные ответы и честно сообщает о неуверенности.
    """

    result: T
    confidence: float = Field(ge=0.0, le=1.0, description="Уверенность модели, 0..1")
    is_confident: bool = Field(description="Превышен ли порог доверия")
    explanation: str = Field(description="Объяснение результата на человеческом языке")
    contributions: list[FeatureContribution] = Field(default_factory=list)
    model_version: str = Field(default="unknown")

    model_config = {"protected_namespaces": ()}


class ErrorResponse(BaseModel):
    detail: str
    code: str = "ai.error"
