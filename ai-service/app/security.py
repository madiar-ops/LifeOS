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


def _as_bytes(value: str) -> bytes:
    """Приводим значение ключа к байтам ровно в том виде, в каком оно шло по сети.

    hmac.compare_digest отказывается сравнивать строки с символами вне ASCII
    и бросает TypeError. Без этого преобразования запрос с ключом на кириллице
    (или любым нелатинским) превращался в 500 вместо честного 401: посторонний
    клиент получал внутреннюю ошибку сервиса вместо отказа в доступе.

    ASGI отдаёт значения заголовков декодированными в latin-1, поэтому обратная
    кодировка в latin-1 возвращает исходные байты запроса. Ожидаемый ключ
    приходит из окружения как обычная строка Python — его кодируем в UTF-8,
    то есть в те же байты, которые отправил бы корректный клиент.
    """
    try:
        return value.encode("latin-1")
    except UnicodeEncodeError:
        return value.encode("utf-8")


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
    if not x_internal_api_key or not hmac.compare_digest(
        _as_bytes(x_internal_api_key), expected.encode("utf-8")
    ):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Недействительный внутренний API-ключ.",
        )
