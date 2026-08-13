# Фаза 2 — Auth (JWT + Refresh с ротацией)

---

## 1. Что добавлено

| Слой | Файлы |
|---|---|
| **Application** | `JwtSettings`, DTO (`RegisterRequest`, `LoginRequest`, `RefreshRequest`, `AuthResponse`, `UserResponse`), интерфейсы (`IPasswordHasher`, `IJwtTokenGenerator`, `IAuthService`), `AuthService`, 3 валидатора |
| **Infrastructure** | `BCryptPasswordHasher`, `JwtTokenGenerator` |
| **API** | `CurrentUserService`, `AuthenticationExtensions`, `ValidationFilter`, `AuthController`, `LifeOS.API.http` |
| **Изменено** | `Program.cs` (UseAuthentication), `DependencyInjection` обоих слоёв, `appsettings.json` (секция `Jwt`), `.csproj` (4 новых пакета) |

Миграция **не нужна** — схема БД не менялась, `RefreshTokens` создана ещё в Фазе 1.

---

## 2. Настройка перед запуском

Нужен секретный ключ подписи (минимум 32 символа). Сгенерировать:

```bash
# PowerShell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))

# bash
openssl rand -base64 48
```

Положить в user-secrets (не в appsettings.json):

```bash
cd backend/src/LifeOS.API
dotnet user-secrets set "Jwt:Key" "<сгенерированный_ключ>"
```

Если ключ не задан — **приложение не запустится**. Это сделано намеренно (`ValidateOnStart`): лучше явный отказ при старте, чем 500-я ошибка у пользователя в проде.

---

## 3. Как работает ротация refresh-токенов

```
Логин          → выдаётся RT1 (в БД: активен)
Refresh(RT1)   → RT1.IsRevoked = true
                 RT1.ReplacedByToken = RT2
                 выдаётся RT2
Refresh(RT1)   → RT1 уже отозван → КОМПРОМЕТАЦИЯ
                 → гасятся ВСЕ токены пользователя
                 → 400 auth.token_reuse_detected
```

**Смысл:** если злоумышленник украл RT1 и воспользовался им, легитимный клиент при следующем refresh предъявит тот же RT1 — и система поймёт, что копия токена гуляет на стороне. Оба будут разлогинены. Без ротации кража оставалась бы незамеченной до истечения срока.

Именно это отвечает на вопрос преподавателя «а что если токен украдут?».

---

## 4. Принятые решения

| Решение | Почему |
|---|---|
| Access 15 мин / Refresh 7 дней | Access нельзя отозвать — только пережить. Короткий срок = маленькое окно для украденного токена |
| Refresh-токен — не JWT, а случайные 64 байта | Его подлинность проверяется только по записи в БД → подделать невозможно в принципе, и его можно отозвать |
| `ClockSkew = TimeSpan.Zero` | По умолчанию ASP.NET даёт 5 минут запаса — это треть жизни нашего access-токена |
| Одинаковая ошибка при неверном email и неверном пароле | Иначе форма логина становится инструментом перебора существующих аккаунтов (user enumeration) |
| BCrypt work factor 12 | ~0.2–0.3 с на хеш: медленно для перебора, приемлемо для живого входа |
| `IPasswordHasher`/`IJwtTokenGenerator` — Singleton | Не хранят состояние |
| Валидация — через `ValidationFilter`, штатная отключена | Один формат ошибки вместо двух. Клиент получает словарь «поле → ошибки» |
| Проверка email до вставки + уникальный индекс | Понятная 409 в обычном случае; индекс — защита от гонки двух одновременных регистраций |
| `logout` идемпотентен | Неизвестный или уже погашенный токен — не ошибка, клиент в любом случае вышел |
| Заголовок `X-Token-Expired` при истечении | Фронт отличает «нужен refresh» от «нужно разлогинить» |
| `ValidateOnStart` для `Jwt:Key` | Отказ при старте вместо 500 в рантайме |

---

## 5. Проверка (чек-лист)

Файл `backend/src/LifeOS.API/LifeOS.API.http` содержит все 10 сценариев — открывается прямо в Visual Studio 2022 и выполняется по кнопке. Либо через Swagger.

- [ ] Приложение стартует (значит `Jwt:Key` задан корректно)
- [ ] `POST /api/auth/register` → **200**, в ответе `accessToken`, `refreshToken`, профиль
- [ ] Повтор той же регистрации → **409** `user.email_taken` ← *это и есть проверка уникального индекса на Email*
- [ ] Регистрация с паролем `123` и email `не-email` → **400** со словарём ошибок по полям
- [ ] `POST /api/auth/login` верные данные → **200**
- [ ] `POST /api/auth/login` неверный пароль → **400**, сообщение НЕ раскрывает, существует ли email
- [ ] `GET /api/auth/me` без токена → **401**
- [ ] `GET /api/auth/me` с токеном → **200**, профиль без `passwordHash`
- [ ] `POST /api/auth/refresh` → **200**, новая пара токенов
- [ ] Повтор refresh с тем же токеном → **400** `auth.token_reuse_detected`
- [ ] В таблице `RefreshTokens`: у старой записи `IsRevoked = true`, заполнен `ReplacedByToken`
- [ ] В таблице `Users`: `PasswordHash` начинается с `$2a$12$` (BCrypt, work factor 12)
- [ ] В Swagger появилась кнопка **Authorize**, после ввода токена `/auth/me` работает

SQL для проверки цепочки ротации:

```sql
SELECT "Token", "IsRevoked", "ReplacedByToken", "ExpiresAt"
FROM "RefreshTokens" ORDER BY "CreatedAt";
```

---

## 6. Что дальше — Фаза 3 (Core CRUD)

1. AutoMapper + профили маппинга
2. `BaseCrudService` с проверкой владения ресурсом (`ForbiddenException` уже есть в Domain)
3. Users/Profile, Goals, Tasks, Finance (Transactions), Health (HealthLogs)
4. Валидаторы на все DTO
5. Пагинация через готовый `PagedResult<T>`
