"""Схемы карьерного модуля."""

from pydantic import BaseModel, Field


class ResumeAnalysisRequest(BaseModel):
    resume_text: str = Field(min_length=50)
    desired_position: str | None = Field(default=None, max_length=200)
    skills: list[str] = Field(default_factory=list)
    language: str = Field(default="ru")


class ResumeAnalysis(BaseModel):
    overall_score: float = Field(ge=0, le=100)
    strengths: list[str] = Field(default_factory=list)
    weaknesses: list[str] = Field(default_factory=list)
    missing_skills: list[str] = Field(default_factory=list)
    suggestions: list[str] = Field(default_factory=list)
    source: str
