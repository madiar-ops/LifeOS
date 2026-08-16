"""Реестр обученных моделей.

Модели загружаются с диска ОДИН раз при старте и живут в памяти процесса.
Загружать артефакт на каждый запрос было бы катастрофой по латентности:
десятки миллисекунд превратились бы в секунды.

TECHNICAL_SPEC §19: «Модель должна обучаться заранее. FastAPI только
делает inference». Этот модуль — граница между обучением и инференсом.
"""

from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

import joblib

from app.config import ARTIFACTS_DIR

logger = logging.getLogger(__name__)


class ModelRegistry:
    """Хранилище загруженных моделей и их метаданных."""

    def __init__(self) -> None:
        self._models: dict[str, Any] = {}
        self._metadata: dict[str, dict] = {}
        self._torch_bundle: dict | None = None

    # ---- Загрузка -----------------------------------------------------

    def load_all(self) -> None:
        """Вызывается один раз при старте приложения (lifespan)."""
        self._load_sklearn("finance", "finance_model")
        self._load_sklearn("health", "health_model")
        self._load_torch()

    def _load_sklearn(self, key: str, filename: str) -> None:
        model_path = ARTIFACTS_DIR / f"{filename}.joblib"
        meta_path = ARTIFACTS_DIR / f"{filename}.json"

        if not model_path.exists():
            # Отсутствие модели не должно ронять весь сервис: остальные
            # эндпоинты обязаны работать. Недоступность конкретной модели
            # честно вернётся как 503 при обращении именно к ней.
            logger.warning(
                "Модель '%s' не найдена (%s). Соответствующий эндпоинт будет недоступен. "
                "Запустите обучение: python -m app.ml.training.train_%s",
                key, model_path, key,
            )
            return

        self._models[key] = joblib.load(model_path)
        self._metadata[key] = self._read_metadata(meta_path)

        logger.info(
            "Модель '%s' загружена (версия %s)",
            key, self._metadata[key].get("version", "unknown"),
        )

    def _load_torch(self) -> None:
        model_path = ARTIFACTS_DIR / "wellbeing_model.pt"
        meta_path = ARTIFACTS_DIR / "wellbeing_model.json"

        if not model_path.exists():
            logger.warning(
                "PyTorch-модель самочувствия не найдена (%s). "
                "Запустите: python -m app.ml.training.train_wellbeing_torch",
                model_path,
            )
            return

        # torch импортируется лениво: если модель не обучена, тяжёлая
        # библиотека не тянется в память впустую.
        import torch

        from app.ml.training.train_wellbeing_torch import WellbeingNet

        bundle = torch.load(model_path, map_location="cpu", weights_only=False)

        model = WellbeingNet(input_size=bundle["input_size"])
        model.load_state_dict(bundle["state_dict"])
        model.eval()  # отключает Dropout — в инференсе он вреден

        self._torch_bundle = {
            "model": model,
            "mean": bundle["feature_mean"],
            "std": bundle["feature_std"],
            "features": bundle["features"],
        }
        self._metadata["wellbeing"] = self._read_metadata(meta_path)

        logger.info("PyTorch-модель самочувствия загружена")

    @staticmethod
    def _read_metadata(path: Path) -> dict:
        if not path.exists():
            return {}
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            logger.error("Повреждён файл метаданных: %s", path)
            return {}

    # ---- Доступ -------------------------------------------------------

    def get(self, key: str) -> Any | None:
        return self._models.get(key)

    def get_metadata(self, key: str) -> dict:
        return self._metadata.get(key, {})

    def get_version(self, key: str) -> str:
        return self._metadata.get(key, {}).get("version", "unknown")

    @property
    def torch_bundle(self) -> dict | None:
        return self._torch_bundle

    def status(self) -> dict[str, bool]:
        """Какие модели реально доступны — отдаётся в /health."""
        return {
            "finance": "finance" in self._models,
            "health": "health" in self._models,
            "wellbeing_torch": self._torch_bundle is not None,
        }


registry = ModelRegistry()
