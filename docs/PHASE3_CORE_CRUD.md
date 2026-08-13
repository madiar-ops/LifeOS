# Фаза 3 — Core CRUD модули

---

## 1. Что добавлено

| Слой | Содержимое |
|---|---|
| **Application** | 21 DTO (Users, Goals, Tasks, Finance, Health, Common), 5 интерфейсов сервисов, 5 сервисов, `MappingProfile` (AutoMapper), 10 валидаторов, `CrudGuard`, `PagedResponse<T>` |
| **API** | `UsersController`, `GoalsController`, `TasksController`, `FinanceController`, `HealthController`; `PingController` (переименован); enum'ы как строки в JSON; `DateOnly` в Swagger |
| **Изменено** | `ValidationFilter` (обработка ошибок привязки), `Program.cs`, `DependencyInjection` (Application), `.csproj` (AutoMapper + EF Core) |

Миграция **не нужна** — схема БД не менялась.

---

## 2. ⚠️ Важное изменение маршрута

`GET /api/health` **больше не проверка живости** — этот маршрут теперь занят модулем Health.

| Было | Стало |
|---|---|
| `GET /api/health` (liveness) | `GET /api/ping` |
| — | `GET /api/health/logs` (модуль здоровья) |
| `GET /health` (health check к БД) | без изменений |

---

## 3. Полный список endpoints фазы

**Users** (все требуют токен)
- `GET /api/users/{id}` — только свой профиль
- `PUT /api/users/profile`
- `PUT /api/users/password` — отзывает все сессии

**Goals**
- `GET /api/goals` — фильтры `status`, `priority`, `search`, `pageNumber`, `pageSize`
- `GET /api/goals/{id}` · `POST /api/goals` · `PUT /api/goals/{id}` · `DELETE /api/goals/{id}`

**Tasks**
- `GET /api/tasks` — фильтры `completed`, `goalId`, `dueBefore`, `search`
- `GET /api/tasks/{id}` · `POST /api/tasks` · `PUT /api/tasks/{id}` · `DELETE /api/tasks/{id}`
- `PATCH /api/tasks/{id}/complete` — переключение чекбокса

**Finance**
- `GET /api/finance/transactions` — фильтры `type`, `category`, `from`, `to`
- `GET|POST|PUT|DELETE /api/finance/transactions/{id}`
- `GET /api/finance/summary?from=&to=&currency=`

**Health**
- `GET /api/health/logs` — фильтры `from`, `to`
- `GET|POST|PUT|DELETE /api/health/logs/{id}`

---

## 4. Принятые решения

| Решение | Почему |
|---|---|
| **`CrudGuard.EnsureOwned` в каждом сервисе** | Защита от IDOR. Без неё любой пользователь подставил бы чужой Id в URL и прочитал чужие данные |
| **Чужая сущность → 404, а не 403** | 403 подтверждал бы, что такой Id существует — это позволяет перебирать чужие записи |
| **Фильтр по `UserId` применяется первым в запросе** | Чужие записи не могут попасть в выборку ни при каком наборе параметров |
| **AutoMapper только Entity → DTO** | Обратно маппим вручную: видно, какие поля клиент вправе задать. Невозможно случайно позволить переписать `UserId`, `CreatedAt` или `Role` лишним полем в JSON |
| **Enum'ы в JSON — строками** | `"InProgress"` вместо `1`. Фронту не нужна копия числовых значений, JSON читается глазами |
| **`GoalId` проверяется на владение при создании задачи** | Иначе можно было бы засорять чужие цели своими задачами |
| **Удаление цели не удаляет задачи** | `ON DELETE SET NULL` из Фазы 1: задача переживает свою цель |
| **`Amount` всегда положительна (`Math.Abs`)** | Знак несёт поле `Type`. Иначе расход в −500 и +500 означали бы одно и то же |
| **Верхняя граница суммы 999 999 999** | Отсекает опечатку в 10 нулей, которая исказила бы все графики |
| **Сводка считает одну валюту за раз** | Конвертации по курсам в MVP нет — складывать KZT и USD было бы враньём в отчёте |
| **`GroupBy` выполняется в PostgreSQL** | В память попадают строки итогов, а не все транзакции пользователя |
| **Доля категории — от суммы своего типа** | «30% на еду» = 30% от расходов, а не от оборота |
| **Дата записи здоровья не редактируется** | Она часть уникального ключа `(UserId, Date)` |
| **Границы в health-валидаторе — физиологические, не «нормальные»** | Задача валидатора отсечь опечатку (вес 700 кг), а не судить о здоровье пользователя |
| **`PageSize` ограничен сверху сотней** | Клиент не может выгрузить всю таблицу одним запросом |
| **`ValidationFilter` обрабатывает и ошибки привязки** | Штатный ответ ModelState подавлен; без этого битый JSON давал бы 500 вместо 400 |
| **`ToLower().Contains()` вместо `EF.Functions.ILike`** | `ILike` — специфика Npgsql. Слой Application не должен знать, какая СУБД под ним |

### Осознанный компромисс: EF Core в слое Application

`LifeOS.Application` ссылается на `Microsoft.EntityFrameworkCore` ради `IQueryable`, `Include` и `ToListAsync`. Строго по канону этого быть не должно.

**Почему так сделано:** альтернатива — тащить каждый фильтр в репозиторий отдельным методом (`GetByUserAndStatusAndPriorityAndSearch...`), что порождает десятки почти одинаковых методов. Точно так же устроен эталонный шаблон Clean Architecture Джейсона Тейлора.

**Что при этом сохранено:** провайдер-специфичных пакетов (Npgsql) в Application **нет** — слой не знает, какая СУБД под ним. `LifeOS.Domain` остаётся полностью чистым, без единой внешней зависимости.

Это честный ответ, если на защите спросят про чистоту слоёв.

---

## 5. Проверка (чек-лист)

Сценарии 11–30 в `backend/src/LifeOS.API/LifeOS.API.http`. Сначала выполни login и подставь токен в переменную `@token`.

**Авторизация и владение**
- [ ] `GET /api/goals` без токена → **401**
- [ ] `GET /api/goals/{чужой-id}` → **404** (не 403)
- [ ] Создать задачу с `goalId` чужой цели → **404**

**Goals и Tasks**
- [ ] `POST /api/goals` → **201** + заголовок `Location`
- [ ] `status: "НеСуществует"` → **400** со словарём ошибок
- [ ] Список целей возвращает `items`, `totalCount`, `totalPages`, `hasNext`
- [ ] У цели видны `totalTasks` и `completedTasks`
- [ ] Задача без `goalId` создаётся успешно
- [ ] `PATCH /api/tasks/{id}/complete` переключает флаг
- [ ] **Удалить цель с задачами** → задачи остались, `goalId` стал `null` ← *проверка ON DELETE SET NULL*

**Finance**
- [ ] Доход и расход создаются, `amount` положительна
- [ ] `amount: -500` и `currency: "ТЕНГЕ"` → **400**
- [ ] `GET /api/finance/summary` → верные `totalIncome`, `totalExpense`, `balance`
- [ ] В `byCategory` проценты внутри каждого типа дают ~100

**Health**
- [ ] Запись создаётся
- [ ] Повтор той же даты → **409** ← *проверка уникального индекса (UserId, Date)*
- [ ] `weight: 700` → **400**
- [ ] Дата в будущем → **400**

**Users**
- [ ] `PUT /api/users/profile` меняет имя
- [ ] `PUT /api/users/password` с неверным текущим → **400**
- [ ] После успешной смены пароля старый refresh-токен не работает → **400**

**Общее**
- [ ] `GET /api/ping` → **200**
- [ ] В Swagger видны все 5 новых контроллеров, enum'ы — выпадающими списками строк
- [ ] `DateOnly` в Swagger отображается как строка `2026-08-12`, а не объект

---

## 6. Что дальше — Фаза 4 (Files)

1. `FirebaseStorageService` — загрузка и удаление объектов
2. Валидация файлов: тип, размер, расширение
3. `/api/files/upload`, `/api/files/{id}`, `DELETE /api/files/{id}`
4. Аватар пользователя (`PUT /api/users/avatar`)
5. Подготовка к Study и Career, которым нужны PDF
