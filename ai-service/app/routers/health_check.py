"""Служебные эндпоинты. Единственные, что доступны без внутреннего ключа —
иначе оркестратор не смог бы проверить живость контейнера."""

from datetime import datetime, timezone

from fastapi import APIRouter

from app.config import get_settings
from app.services.model_registry import registry

router = APIRouter(tags=["Service"])


@router.get("/ping")
async def ping() -> dict:
    settings = get_settings()
    return {
        "service": settings.app_name,
        "status": "healthy",
        "environment": settings.environment,
        "utc_time": datetime.now(timezone.utc).isoformat(),
    }


@router.get("/health")
async def health() -> dict:
    """Готовность: какие модели реально загружены.

    Сервис считается работоспособным даже без части моделей —
    недоступные эндпоинты вернут 503 при обращении именно к ним.
    """
    settings = get_settings()
    models = registry.status()

    return {
        "status": "healthy",
        "models": models,
        "all_models_loaded": all(models.values()),
        "llm_configured": settings.llm_enabled,
        "confidence_threshold": settings.confidence_threshold,
    }
