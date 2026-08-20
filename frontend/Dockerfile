# ============================================================
# LifeOS Frontend — сборка Vite + раздача через nginx
#
# Основной способ деплоя фронтенда — Vercel (см. vercel.json).
# Этот образ нужен для полного локального стека через docker compose
# и как запасной путь, если фронтенд придётся разместить рядом с backend.
# ============================================================

FROM node:22-alpine AS build

WORKDIR /app

# npm ci вместо npm install: ставит строго то, что зафиксировано
# в package-lock.json. Сборка становится воспроизводимой.
COPY package*.json ./
RUN npm ci

COPY . .

# Адрес API вшивается в бандл на этапе сборки — у Vite нет
# рантайм-конфигурации. Для другого окружения нужен новый образ.
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build


# ------------------------------------------------------------
FROM nginx:1.27-alpine AS runtime

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
