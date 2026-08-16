"""Обучение модели прогноза расходов (scikit-learn).

Задача: регрессия — предсказать расход следующего месяца.
Модель: GradientBoostingRegressor.

Почему не нейросеть: данные табличные и их немного. На таких задачах
градиентный бустинг стабильно обходит нейросети, обучается за секунды
и даёт готовые feature_importances_ для объяснимости. Использовать
PyTorch здесь было бы демонстрацией ради демонстрации.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone

import joblib
import numpy as np
import pandas as pd
from sklearn.ensemble import GradientBoostingRegressor
from sklearn.metrics import mean_absolute_error, r2_score
from sklearn.model_selection import train_test_split

from app.config import ARTIFACTS_DIR, DATASETS_DIR

MODEL_PATH = ARTIFACTS_DIR / "finance_model.joblib"
META_PATH = ARTIFACTS_DIR / "finance_model.json"
DATASET_PATH = DATASETS_DIR / "finance_history.csv"

FEATURES = ["income", "prev_expense", "expense_ratio_prev", "month_of_year"]
TARGET = "expense"

MODEL_VERSION = "finance-gbr-1.0"


def load_dataset() -> pd.DataFrame:
    if not DATASET_PATH.exists():
        raise FileNotFoundError(
            f"Датасет не найден: {DATASET_PATH}. "
            "Сначала выполните: python -m app.ml.training.generate_datasets"
        )
    return pd.read_csv(DATASET_PATH)


def main() -> None:
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)

    df = load_dataset()
    X = df[FEATURES]
    y = df[TARGET]

    # Разделение до обучения: качество меряется на данных, которых
    # модель не видела, иначе метрика ничего не значит.
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42
    )

    model = GradientBoostingRegressor(
        n_estimators=300,
        learning_rate=0.05,
        max_depth=3,
        subsample=0.9,
        random_state=42,
    )
    model.fit(X_train, y_train)

    predictions = model.predict(X_test)
    mae = float(mean_absolute_error(y_test, predictions))
    r2 = float(r2_score(y_test, predictions))

    # Типичная ошибка модели нужна в рантайме: по ней считается
    # уверенность конкретного прогноза.
    residual_std = float(np.std(y_test - predictions))

    joblib.dump(model, MODEL_PATH)

    metadata = {
        "version": MODEL_VERSION,
        "algorithm": "GradientBoostingRegressor",
        "features": FEATURES,
        "target": TARGET,
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "train_rows": int(len(X_train)),
        "test_rows": int(len(X_test)),
        "metrics": {"mae": round(mae, 2), "r2": round(r2, 4)},
        "residual_std": round(residual_std, 2),
        "feature_importances": {
            name: round(float(value), 4)
            for name, value in zip(FEATURES, model.feature_importances_)
        },
    }
    META_PATH.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Модель сохранена: {MODEL_PATH}")
    print(f"MAE: {mae:,.2f} | R2: {r2:.4f} | residual_std: {residual_std:,.2f}")
    print(f"Важность признаков: {metadata['feature_importances']}")


if __name__ == "__main__":
    main()
