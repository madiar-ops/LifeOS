# ============================================================
# LifeOS Backend — многоступенчатая сборка
#
# В финальный образ не попадают ни SDK (≈700 МБ), ни исходники,
# ни NuGet-кэш: только runtime и скомпилированные сборки.
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Сначала копируем ТОЛЬКО файлы проектов и восстанавливаем пакеты.
# Docker кэширует этот слой: пока зависимости не менялись, restore
# при повторных сборках не выполняется заново.
COPY Directory.Build.props ./
COPY LifeOS.sln ./
COPY src/LifeOS.Domain/*.csproj src/LifeOS.Domain/
COPY src/LifeOS.Application/*.csproj src/LifeOS.Application/
COPY src/LifeOS.Infrastructure/*.csproj src/LifeOS.Infrastructure/
COPY src/LifeOS.API/*.csproj src/LifeOS.API/

RUN dotnet restore src/LifeOS.API/LifeOS.API.csproj

# Теперь исходники: их изменение инвалидирует только слои ниже.
COPY src/ src/

RUN dotnet publish src/LifeOS.API/LifeOS.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


# ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Непривилегированный пользователь: процесс в контейнере не должен быть root.
# Если злоумышленник выполнит код внутри контейнера, прав на систему у него не будет.
RUN adduser --disabled-password --gecos "" --uid 1000 lifeos \
    && mkdir -p /app/wwwroot/uploads \
    && chown -R lifeos:lifeos /app

COPY --from=build --chown=lifeos:lifeos /app/publish .

USER lifeos

# PaaS передаёт порт переменной PORT — слушаем именно её.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# /api/ping не обращается к БД: контейнер считается живым,
# даже когда база временно недоступна. Это разные вещи —
# «приложение упало» и «база не отвечает».
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD ["dotnet", "--version"]

ENTRYPOINT ["dotnet", "LifeOS.API.dll"]
