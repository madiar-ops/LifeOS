"""Обучение классификатора настроения (scikit-learn).

Задача: многоклассовая классификация — предсказать настроение (1..5)
по сну, шагам и воде.
Модель: RandomForestClassifier в пайплайне со StandardScaler.

Почему пайплайн, а не отдельный scaler: масштабирование сохраняется
внутри артефакта. Это исключает классическую ошибку, когда в проде
данные подаются в модель без той же нормализации, что при обучении.

predict_proba даёт распределение вероятностей — из него берётся
уверенность конкретного предсказания.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone

import joblib
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, f1_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from app.config import ARTIFACTS_DIR, DATASETS_DIR

MODEL_PATH = ARTIFACTS_DIR / "health_model.joblib"
META_PATH = ARTIFACTS_DIR / "health_model.json"
DATASET_PATH = DATASETS_DIR / "health_logs.csv"

FEATURES = ["sleep_hours", "steps", "water_ml"]
TARGET = "mood"

MODEL_VERSION = "health-rf-1.0"


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

    # stratify сохраняет пропорции классов: редкие значения настроения
    # иначе могли бы полностью исчезнуть из тестовой выборки.
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )

    model = Pipeline(
        steps=[
            ("scaler", StandardScaler()),
            (
                "classifier",
                RandomForestClassifier(
                    n_estimators=250,
                    max_depth=12,
                    min_samples_leaf=5,
                    random_state=42,
                    n_jobs=-1,
                ),
            ),
        ]
    )
    model.fit(X_train, y_train)

    predictions = model.predict(X_test)
    accuracy = float(accuracy_score(y_test, predictions))
    f1 = float(f1_score(y_test, predictions, average="weighted"))

    joblib.dump(model, MODEL_PATH)

    importances = model.named_steps["classifier"].feature_importances_

    metadata = {
        "version": MODEL_VERSION,
        "algorithm": "RandomForestClassifier + StandardScaler",
        "features": FEATURES,
        "target": TARGET,
        "classes": [int(c) for c in model.named_steps["classifier"].classes_],
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "train_rows": int(len(X_train)),
        "test_rows": int(len(X_test)),
        "metrics": {"accuracy": round(accuracy, 4), "f1_weighted": round(f1, 4)},
        "feature_importances": {
            name: round(float(value), 4) for name, value in zip(FEATURES, importances)
        },
    }
    META_PATH.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Модель сохранена: {MODEL_PATH}")
    print(f"Accuracy: {accuracy:.4f} | F1(weighted): {f1:.4f}")
    print(f"Важность признаков: {metadata['feature_importances']}")


if __name__ == "__main__":
    main()
