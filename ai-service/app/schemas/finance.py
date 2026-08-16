"""Схемы финансового модуля."""

from pydantic import BaseModel, Field


class MonthlyTotal(BaseModel):
    """Агрегат за один месяц. Backend передаёт уже посчитанные суммы —
    AI-сервис не ходит в базу и не знает про неё ничего."""

    month: str = Field(description="Месяц в формате YYYY-MM")
    income: float = Field(ge=0)
    expense: float = Field(ge=0)


class CategoryTotal(BaseModel):
    category: str
    amount: float = Field(ge=0)


class FinanceAnalysisRequest(BaseModel):
    history: list[MonthlyTotal] = Field(
        min_length=1,
        description="История по месяцам, от старых к новым",
    )
    categories: list[CategoryTotal] = Field(default_factory=list)
    currency: str = Field(default="KZT", max_length=3)


class FinanceForecast(BaseModel):
    predicted_expense: float = Field(description="Прогноз расходов на следующий месяц")
    predicted_balance: float = Field(description="Ожидаемый баланс при текущем доходе")
    trend: str = Field(description="rising | stable | falling")
    top_category: str | None = Field(default=None)
    savings_rate: float = Field(description="Доля дохода, остающаяся неизрасходованной, 0..1")
