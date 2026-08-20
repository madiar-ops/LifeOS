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
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONPATH=/app:/home/aiservice/.local/lib/python3.12/site-packages

# Модели обучаются НА ЭТАПЕ СБОРКИ образа, а не копируются с машины
# разработчика и не обучаются при старте.
#
# • Копировать артефакты нельзя: образ зависел бы от того, что лежит
#   у конкретного человека на диске, и перестал бы быть воспроизводимым.
# • Обучать при старте нельзя: PaaS убивает контейнер, если тот не начал
#   отвечать на порт за отведённое время.
#
# Обучение детерминировано (фиксированный seed), поэтому образ,
# собранный где угодно, содержит те же самые веса.
RUN python -m app.ml.training.generate_datasets \
    && python -m app.ml.training.train_finance \
    && python -m app.ml.training.train_health \
    && python -m app.ml.training.train_wellbeing_torch \
    && rm -rf app/ml/datasets/*.csv

USER aiservice

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD python -c "import httpx; httpx.get('http://localhost:8000/ping', timeout=3).raise_for_status()"

# PaaS назначает порт переменной окружения PORT.
CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8000}"]
