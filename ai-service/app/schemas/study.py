"""Схемы учебного модуля."""

from pydantic import BaseModel, Field


class StudySummaryRequest(BaseModel):
    text: str = Field(min_length=50, description="Извлечённый из PDF текст")
    max_sentences: int = Field(default=7, ge=3, le=20)
    language: str = Field(default="ru")


class StudySummary(BaseModel):
    summary: str
    key_points: list[str] = Field(default_factory=list)
    source: str = Field(description="llm | extractive — каким способом получен результат")


class QuizQuestion(BaseModel):
    question: str
    options: list[str] = Field(min_length=2)
    correct_index: int = Field(ge=0)
    explanation: str = ""


class QuizRequest(BaseModel):
    text: str = Field(min_length=50)
    question_count: int = Field(default=5, ge=1, le=15)
    language: str = Field(default="ru")


class QuizResult(BaseModel):
    questions: list[QuizQuestion]
    source: str
