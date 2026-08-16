"""Показательная модель на PyTorch: регрессия интегрального самочувствия.

Зачем она, если табличные задачи уже закрыты scikit-learn: MASTER_GUIDE
требует PyTorch в стеке, и одна осмысленная нейросетевая модель лучше,
чем перевод всего проекта на PyTorch ради галочки. Здесь MLP решает
задачу, где нелинейность действительно оправдана — интегральная оценка
самочувствия по нескольким взаимодействующим факторам.

Честный ответ на защите: на табличных данных такого объёма бустинг
работает не хуже. Эта модель показывает владение инструментом.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone

import numpy as np
import pandas as pd
import torch
from torch import nn
from torch.utils.data import DataLoader, TensorDataset

from app.config import ARTIFACTS_DIR, DATASETS_DIR

MODEL_PATH = ARTIFACTS_DIR / "wellbeing_model.pt"
META_PATH = ARTIFACTS_DIR / "wellbeing_model.json"
DATASET_PATH = DATASETS_DIR / "health_logs.csv"

FEATURES = ["sleep_hours", "steps", "water_ml"]
MODEL_VERSION = "wellbeing-mlp-1.0"

EPOCHS = 40
BATCH_SIZE = 256
LEARNING_RATE = 1e-3


class WellbeingNet(nn.Module):
    """Небольшой MLP: 3 -> 32 -> 16 -> 1.

    Dropout защищает от переобучения — сеть с тысячами параметров
    на таком датасете легко запомнила бы обучающую выборку целиком.
    """

    def __init__(self, input_size: int = 3) -> None:
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(input_size, 32),
            nn.ReLU(),
            nn.Dropout(0.15),
            nn.Linear(32, 16),
            nn.ReLU(),
            nn.Linear(16, 1),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)


def build_target(df: pd.DataFrame) -> np.ndarray:
    """Целевая переменная: настроение 1..5, растянутое в шкалу 0..100."""
    return ((df["mood"].to_numpy(dtype=np.float32) - 1.0) / 4.0) * 100.0


def main() -> None:
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)

    if not DATASET_PATH.exists():
        raise FileNotFoundError(
            f"Датасет не найден: {DATASET_PATH}. "
            "Сначала выполните: python -m app.ml.training.generate_datasets"
        )

    torch.manual_seed(42)
    np.random.seed(42)

    df = pd.read_csv(DATASET_PATH)
    X = df[FEATURES].to_numpy(dtype=np.float32)
    y = build_target(df).reshape(-1, 1)

    # Нормализация обязательна: шаги измеряются тысячами, а сон — единицами.
    # Без неё градиенты по признакам различались бы на порядки.
    mean = X.mean(axis=0)
    std = X.std(axis=0)
    std[std == 0] = 1.0
    X_scaled = (X - mean) / std

    split = int(len(X_scaled) * 0.8)
    indices = np.random.permutation(len(X_scaled))
    train_idx, test_idx = indices[:split], indices[split:]

    train_loader = DataLoader(
        TensorDataset(
            torch.tensor(X_scaled[train_idx]),
            torch.tensor(y[train_idx], dtype=torch.float32),
        ),
        batch_size=BATCH_SIZE,
        shuffle=True,
    )

    X_test = torch.tensor(X_scaled[test_idx])
    y_test = torch.tensor(y[test_idx], dtype=torch.float32)

    model = WellbeingNet(input_size=len(FEATURES))
    criterion = nn.MSELoss()
    optimizer = torch.optim.Adam(model.parameters(), lr=LEARNING_RATE)

    for epoch in range(1, EPOCHS + 1):
        model.train()
        epoch_loss = 0.0

        for batch_x, batch_y in train_loader:
            optimizer.zero_grad()
            loss = criterion(model(batch_x), batch_y)
            loss.backward()
            optimizer.step()
            epoch_loss += loss.item()

        if epoch % 10 == 0 or epoch == 1:
            model.eval()
            with torch.no_grad():
                test_loss = criterion(model(X_test), y_test).item()
            print(
                f"Эпоха {epoch:>3}/{EPOCHS} | "
                f"train MSE {epoch_loss / len(train_loader):8.3f} | "
                f"test MSE {test_loss:8.3f}"
            )

    model.eval()
    with torch.no_grad():
        predictions = model(X_test)
        mae = torch.mean(torch.abs(predictions - y_test)).item()
        rmse = torch.sqrt(criterion(predictions, y_test)).item()

    # Сохраняем веса вместе с параметрами нормализации: без mean и std
    # модель в проде получала бы данные в другом масштабе и выдавала мусор.
    torch.save(
        {
            "state_dict": model.state_dict(),
            "input_size": len(FEATURES),
            "feature_mean": mean.tolist(),
            "feature_std": std.tolist(),
            "features": FEATURES,
        },
        MODEL_PATH,
    )

    metadata = {
        "version": MODEL_VERSION,
        "algorithm": "PyTorch MLP (3-32-16-1)",
        "features": FEATURES,
        "target": "wellbeing_score (0..100)",
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "epochs": EPOCHS,
        "train_rows": int(len(train_idx)),
        "test_rows": int(len(test_idx)),
        "metrics": {"mae": round(mae, 3), "rmse": round(rmse, 3)},
    }
    META_PATH.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Модель сохранена: {MODEL_PATH}")
    print(f"MAE: {mae:.3f} | RMSE: {rmse:.3f}")


if __name__ == "__main__":
    main()
