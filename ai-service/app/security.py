"""Защита внутреннего канала ASP.NET Core -> FastAPI.

Архитектурное правило: FastAPI никогда не общается с React напрямую и не
знает про пользовательские JWT. Он доверяет только backend'у, а тот
предъявляет общий секрет в заголовке X-Internal-Api-Key.

Так AI-сервис можно держать в приватной сети без публичного доступа,
а даже при случайной публикации ключ отсечёт посторонние запросы.
"""

import hmac

from fastapi import Depends, Header, HTTPException, status

from app.config import Settings, get_settings

API_KEY_HEADER = "X-Internal-Api-Key"


async def verify_internal_api_key(
    x_internal_api_key: str | None = Header(default=None, alias=API_KEY_HEADER),
    settings: Settings = Depends(get_settings),
) -> None:
    """Проверка ключа. Подключается как зависимость ко всем защищённым роутерам."""

    expected = settings.internal_api_key.strip()

    if not expected:
        # Сервис без ключа принимал бы запросы от кого угодно — это отказ,
        # а не «удобный режим разработки».
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="INTERNAL_API_KEY не сконфигурирован на AI-сервисе.",
        )

    # compare_digest вместо == : сравнение за постоянное время,
    # чтобы по времени ответа нельзя было подбирать ключ посимвольно.
    if not x_internal_api_key or not hmac.compare_digest(x_internal_api_key, expected):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Недействительный внутренний API-ключ.",
        )
