# Многоступенчатая сборка: в финальный образ не попадают компиляторы
# и кэш pip, оставшиеся от установки зависимостей.
FROM python:3.12-slim AS builder

WORKDIR /build

RUN apt-get update && apt-get install -y --no-install-recommends \
        build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .

# CPU-версия torch: GPU не используется, образ меньше на несколько гигабайт.
RUN pip install --no-cache-dir --user \
        --extra-index-url https://download.pytorch.org/whl/cpu \
        -r requirements.txt


FROM python:3.12-slim

WORKDIR /app

# Непривилегированный пользователь: процесс в контейнере не должен быть root.
RUN useradd --create-home --uid 1000 aiservice

COPY --from=builder /root/.local /home/aiservice/.local
COPY --chown=aiservice:aiservice app ./app

ENV PATH=/home/aiservice/.local/bin:$PATH \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

USER aiservice

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD python -c "import httpx; httpx.get('http://localhost:8000/ping', timeout=3).raise_for_status()"

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
