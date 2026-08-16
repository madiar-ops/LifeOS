"""Конфигурация AI-сервиса.

Все настройки читаются из переменных окружения — никаких секретов в коде.
Pydantic Settings валидирует их при старте: неверная конфигурация должна
ронять сервис сразу, а не превращаться в 500-ю ошибку у пользователя.
"""

from functools import lru_cache
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

BASE_DIR = Path(__file__).resolve().parent
ARTIFACTS_DIR = BASE_DIR / "ml" / "artifacts"
DATASETS_DIR = BASE_DIR / "ml" / "datasets"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    app_name: str = "LifeOS AI Service"
    environment: str = Field(default="Development")
    debug: bool = Field(default=False)

    # Общий секрет канала ASP.NET Core -> FastAPI.
    # FastAPI не публикуется наружу: единственный легитимный клиент — backend.
    internal_api_key: str = Field(default="", alias="INTERNAL_API_KEY")

    # Порог уверенности. Ниже него ответ помечается как ненадёжный —
    # требование MASTER_GUIDE: "если AI не уверен, он сообщает об этом".
    confidence_threshold: float = Field(default=0.60, ge=0.0, le=1.0)

    # Внешний LLM для суммаризации и анализа резюме.
    # Пусто — сервис переключается на локальный извлекающий алгоритм.
    llm_api_key: str = Field(default="", alias="LLM_API_KEY")
    llm_model: str = Field(default="claude-sonnet-4-6", alias="LLM_MODEL")
    llm_timeout_seconds: int = Field(default=60)

    cors_origins: str = Field(default="")

    @property
    def is_development(self) -> bool:
        return self.environment.lower() == "development"

    @property
    def llm_enabled(self) -> bool:
        return bool(self.llm_api_key.strip())


@lru_cache
def get_settings() -> Settings:
    """Кэшируем: настройки читаются один раз за жизнь процесса."""
    return Settings()
