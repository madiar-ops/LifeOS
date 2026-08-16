"""Финансовый анализ."""

from fastapi import APIRouter, Depends

from app.schemas.common import AIResponse
from app.schemas.finance import FinanceAnalysisRequest, FinanceForecast
from app.security import verify_internal_api_key
from app.services import finance_service

router = APIRouter(
    prefix="/finance",
    tags=["Finance"],
    dependencies=[Depends(verify_internal_api_key)],
)


@router.post("/analysis", response_model=AIResponse[FinanceForecast])
async def analyze(request: FinanceAnalysisRequest) -> AIResponse[FinanceForecast]:
    """Прогноз расходов на следующий месяц и оценка динамики.

    Возвращает уверенность и разбор вклада признаков — пользователь
    видит не только цифру, но и на чём она основана.
    """
    return finance_service.analyze(request)
