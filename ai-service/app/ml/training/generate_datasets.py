"""Генерация обучающих датасетов.

Почему синтетика, а не сразу Kaggle: датасеты Kaggle требуют аккаунта и
ручной загрузки, и без них проект не собрался бы «из коробки» ни у кого,
включая комиссию. Здесь данные генерируются по явно заданным
закономерностям — они детерминированы (фиксированный seed) и
воспроизводимы.

Заменить на реальные данные Kaggle просто: положить CSV с теми же
колонками в app/ml/datasets/ и запустить обучение — скрипты обучения
сначала ищут реальный файл и только потом откатываются к синтетике.
"""

from __future__ import annotations

import numpy as np
import pandas as pd

from app.config import DATASETS_DIR

RANDOM_SEED = 42

FINANCE_CSV = DATASETS_DIR / "finance_history.csv"
HEALTH_CSV = DATASETS_DIR / "health_logs.csv"


def generate_finance(n_users: int = 800, months: int = 12) -> pd.DataFrame:
    """Финансовая история.

    Заложенная зависимость: расход следующего месяца определяется
    предыдущими расходами, доходом и сезонностью — то есть у модели
    действительно есть что выучить, а не белый шум.
    """
    rng = np.random.default_rng(RANDOM_SEED)
    rows: list[dict] = []

    for user in range(n_users):
        base_income = rng.normal(250_000, 80_000)
        base_income = max(base_income, 60_000)

        # Склонность к тратам — устойчивая черта пользователя.
        spend_ratio = np.clip(rng.normal(0.72, 0.14), 0.30, 1.10)
        prev_expense = base_income * spend_ratio

        for month in range(months):
            income = max(base_income * rng.normal(1.0, 0.06), 30_000)

            # Декабрь и сентябрь дороже: праздники и начало учебного года.
            seasonal = 1.25 if month % 12 == 11 else 1.12 if month % 12 == 8 else 1.0

            expense = (
                0.55 * prev_expense
                + 0.35 * income * spend_ratio
                + rng.normal(0, income * 0.05)
            ) * seasonal
            expense = float(np.clip(expense, 10_000, income * 1.6))

            rows.append(
                {
                    "user_id": user,
                    "month_index": month,
                    "income": round(income, 2),
                    "prev_expense": round(prev_expense, 2),
                    "expense_ratio_prev": round(prev_expense / income, 4),
                    "month_of_year": month % 12 + 1,
                    "expense": round(expense, 2),
                }
            )
            prev_expense = expense

    return pd.DataFrame(rows)


def generate_health(n_users: int = 900, days: int = 30) -> pd.DataFrame:
    """Дневник здоровья.

    Заложенная зависимость: настроение растёт от сна, шагов и воды,
    падает при недосыпе. Связь нелинейная — сон полезен до определённого
    предела, что даёт модели нетривиальную задачу.
    """
    rng = np.random.default_rng(RANDOM_SEED + 1)
    rows: list[dict] = []

    for user in range(n_users):
        sleep_base = np.clip(rng.normal(7.0, 1.3), 3.5, 10.5)
        activity_base = np.clip(rng.normal(7500, 3000), 500, 22_000)

        for day in range(days):
            sleep = float(np.clip(rng.normal(sleep_base, 0.9), 2.0, 12.0))
            steps = int(np.clip(rng.normal(activity_base, 2200), 0, 30_000))
            water = int(np.clip(rng.normal(1900, 600), 200, 5000))

            # Полезность сна растёт до ~8 часов, дальше выходит на плато.
            sleep_effect = 2.2 * np.tanh((sleep - 5.0) / 2.0)
            steps_effect = 1.1 * np.tanh((steps - 4000) / 5000)
            water_effect = 0.7 * np.tanh((water - 1200) / 900)

            score = 3.0 + sleep_effect + steps_effect + water_effect + rng.normal(0, 0.45)
            mood = int(np.clip(round(score), 1, 5))

            rows.append(
                {
                    "user_id": user,
                    "day_index": day,
                    "sleep_hours": round(sleep, 2),
                    "steps": steps,
                    "water_ml": water,
                    "mood": mood,
                }
            )

    return pd.DataFrame(rows)


def main() -> None:
    DATASETS_DIR.mkdir(parents=True, exist_ok=True)

    finance = generate_finance()
    finance.to_csv(FINANCE_CSV, index=False)
    print(f"Финансовый датасет: {len(finance)} строк -> {FINANCE_CSV}")

    health = generate_health()
    health.to_csv(HEALTH_CSV, index=False)
    print(f"Датасет здоровья: {len(health)} строк -> {HEALTH_CSV}")


if __name__ == "__main__":
    main()
