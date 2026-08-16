"""Карьерный модуль."""

from fastapi import APIRouter, Depends

from app.schemas.career import ResumeAnalysis, ResumeAnalysisRequest
from app.schemas.common import AIResponse
from app.security import verify_internal_api_key
from app.services import career_service

router = APIRouter(
    prefix="/career",
    tags=["Career"],
    dependencies=[Depends(verify_internal_api_key)],
)


@router.post("/resume-analysis", response_model=AIResponse[ResumeAnalysis])
async def analyze_resume(request: ResumeAnalysisRequest) -> AIResponse[ResumeAnalysis]:
    """Разбор резюме: сильные и слабые стороны, недостающие навыки."""
    return await career_service.analyze(request)
