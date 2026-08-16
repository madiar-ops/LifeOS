"""Анализ показателей здоровья."""

from fastapi import APIRouter, Depends

from app.schemas.common import AIResponse
from app.schemas.health import HealthAnalysisRequest, HealthAssessment
from app.security import verify_internal_api_key
from app.services import health_service

router = APIRouter(
    prefix="/health-analysis",
    tags=["Health"],
    dependencies=[Depends(verify_internal_api_key)],
)


@router.post("", response_model=AIResponse[HealthAssessment])
async def analyze(request: HealthAnalysisRequest) -> AIResponse[HealthAssessment]:
    """Оценка самочувствия, прогноз настроения и факторы риска."""
    return health_service.analyze(request)
