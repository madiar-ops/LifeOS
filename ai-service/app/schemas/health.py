"""Схемы модуля здоровья."""

from pydantic import BaseModel, Field


class HealthEntry(BaseModel):
    """Одна суточная запись. Названия совпадают с сущностью HealthLog
    в backend — но это отдельный контракт, а не общая модель:
    сервисы должны эволюционировать независимо."""

    date: str = Field(description="Дата в формате YYYY-MM-DD")
    sleep_hours: float | None = Field(default=None, ge=0, le=24)
    water_ml: int = Field(default=0, ge=0)
    steps: int = Field(default=0, ge=0)
    weight: float | None = Field(default=None, gt=0)
    mood: int | None = Field(default=None, ge=1, le=5, description="1..5, как MoodLevel")


class HealthAnalysisRequest(BaseModel):
    entries: list[HealthEntry] = Field(min_length=1)


class HealthAssessment(BaseModel):
    wellbeing_score: float = Field(ge=0, le=100, description="Интегральная оценка самочувствия")
    predicted_mood: int = Field(ge=1, le=5, description="Прогноз настроения на завтра")
    risk_factors: list[str] = Field(default_factory=list)
    recommendations: list[str] = Field(default_factory=list)
