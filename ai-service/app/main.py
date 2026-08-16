"""Точка входа AI-сервиса LifeOS.

Роль сервиса по архитектуре: только инференс. Он не хранит пользователей,
не знает про JWT, не подключается к PostgreSQL и не общается с React.
Единственный клиент — ASP.NET Core, предъявляющий внутренний ключ.
"""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.config import get_settings
from app.routers import career, finance, health_analysis, health_check, study
from app.services.model_registry import registry

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Модели загружаются один раз при старте.

    Загрузка на каждый запрос стоила бы сотни миллисекунд — это главное
    архитектурное отличие инференс-сервиса от обучающего скрипта.
    """
    settings = get_settings()

    logger.info("Запуск %s (%s)...", settings.app_name, settings.environment)

    registry.load_all()

    status = registry.status()
    if not all(status.values()):
        missing = [name for name, loaded in status.items() if not loaded]
        logger.warning(
            "Не загружены модели: %s. Соответствующие эндпоинты вернут 503. "
            "Обучение: см. README.md",
            ", ".join(missing),
        )

    if not settings.llm_enabled:
        logger.info("LLM_API_KEY не задан — Study и Career работают на локальных алгоритмах.")

    yield

    logger.info("Остановка AI-сервиса.")


def create_app() -> FastAPI:
    settings = get_settings()

    app = FastAPI(
        title=settings.app_name,
        version="1.0.0",
        description=(
            "AI-микросервис платформы LifeOS. "
            "Принимает запросы только от ASP.NET Core по внутреннему ключу."
        ),
        lifespan=lifespan,
        # Документация только в разработке: в проде сервис приватный,
        # и публиковать схему его API незачем.
        docs_url="/docs" if settings.is_development else None,
        redoc_url=None,
        openapi_url="/openapi.json" if settings.is_development else None,
    )

    # CORS по умолчанию пуст: браузер не должен обращаться сюда напрямую.
    # Список заполняется только если сервис временно открывают для отладки.
    origins = [o.strip() for o in settings.cors_origins.split(",") if o.strip()]
    if origins:
        app.add_middleware(
            CORSMiddleware,
            allow_origins=origins,
            allow_credentials=False,
            allow_methods=["POST", "GET"],
            allow_headers=["*"],
        )

    app.include_router(health_check.router)
    app.include_router(finance.router)
    app.include_router(health_analysis.router)
    app.include_router(study.router)
    app.include_router(career.router)

    @app.exception_handler(Exception)
    async def unhandled_exception_handler(request: Request, exc: Exception) -> JSONResponse:
        """Единый обработчик — зеркало GlobalExceptionMiddleware в backend.

        Внутренние детали наружу уходят только в разработке.
        """
        logger.exception("Необработанная ошибка при %s %s", request.method, request.url.path)

        return JSONResponse(
            status_code=500,
            content={
                "detail": str(exc) if settings.is_development else "Внутренняя ошибка AI-сервиса.",
                "code": "ai.internal_error",
            },
        )

    return app


app = create_app()
