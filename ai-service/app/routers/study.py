"""Учебный модуль."""

from fastapi import APIRouter, Depends

from app.schemas.common import AIResponse
from app.schemas.study import QuizRequest, QuizResult, StudySummary, StudySummaryRequest
from app.security import verify_internal_api_key
from app.services import study_service

router = APIRouter(
    prefix="/study",
    tags=["Study"],
    dependencies=[Depends(verify_internal_api_key)],
)


@router.post("/summary", response_model=AIResponse[StudySummary])
async def summarize(request: StudySummaryRequest) -> AIResponse[StudySummary]:
    """Конспект учебного материала.

    При наличии ключа LLM — генеративный конспект, иначе извлекающий
    (выбор ключевых предложений исходного текста).
    """
    return await study_service.summarize(request)


@router.post("/quiz", response_model=AIResponse[QuizResult])
async def generate_quiz(request: QuizRequest) -> AIResponse[QuizResult]:
    """Генерация проверочного теста. Требует ключ внешней языковой модели."""
    return await study_service.generate_quiz(request)
