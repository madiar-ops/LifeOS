# Фаза 4 — Files (Firebase Storage)

---

## 1. Что добавлено

| Слой | Содержимое |
|---|---|
| **Application** | `FileStorageSettings`, `FileUploadData`, `StorageUploadResult`, `FileValidationRules`, `IFileStorageService`, `IFileService`, `FileService`, `FileResponse`, `FileQueryParams` |
| **Infrastructure** | `FirebaseStorageService`, `LocalFileStorageService`, `StoragePathBuilder` |
| **API** | `FilesController`, `PUT /api/users/avatar`, раздача статики, лимит тела запроса |
| **Изменено** | `MappingProfile`, DI обоих слоёв, `appsettings.json` (секция `FileStorage`), `.gitignore`, `.env.example` |

Миграция **не нужна** — таблица `Files` создана ещё в Фазе 1.

---

## 2. Два провайдера хранилища

Выбор происходит автоматически при старте:

| Условие | Провайдер |
|---|---|
| `FileStorage:Bucket` пуст **или** `ForceLocal = true` | `LocalDisk` — папка `wwwroot/uploads` |
| `Bucket` задан | `FirebaseStorage` |

**Зачем нужен локальный.** Чтобы разработка Study и Career не блокировалась настройкой Firebase — можно начинать сразу. В логах при старте будет предупреждение о том, что активен локальный режим.

**В прод локальный не годится:** на Render и подобных платформах файловая система эфемерна и очищается при каждом рестарте.

### Настройка Firebase

1. Firebase Console → Storage → создать bucket (имя вида `lifeos-xxxx.appspot.com`).
2. Project Settings → Service accounts → **Generate new private key** → скачается JSON.
3. Положить JSON вне репозитория и указать путь:

```bash
cd backend/src/LifeOS.API
dotnet user-secrets set "FileStorage:Bucket" "lifeos-xxxx.appspot.com"
dotnet user-secrets set "FileStorage:CredentialsPath" "D:\secrets\lifeos-firebase.json"
```

Для облачного деплоя файл положить некуда — передаётся содержимое строкой через `FileStorage__CredentialsJson`.

---

## 3. Endpoints

- `GET /api/files` — фильтр `module`, пагинация
- `GET /api/files/{id}` — метаданные
- `POST /api/files/upload?module=Study` — `multipart/form-data`, поле `file`
- `DELETE /api/files/{id}`
- `PUT /api/users/avatar` — `multipart/form-data`, поле `file`

---

## 4. Принятые решения

| Решение | Почему |
|---|---|
| **Проверка сигнатуры файла («магические числа»)** | `Content-Type` присылает клиент, подделать его тривиально — достаточно переименовать `.exe` в `.pdf`. Сверка первых байтов (`%PDF`, `FF D8 FF`, PNG-заголовок) — единственный надёжный способ |
| **Три уровня проверки: MIME → расширение → сигнатура** | Каждый по отдельности обходится, вместе — практически нет |
| **Разные разрешённые типы по модулям** | Study и Career принимают только PDF: их содержимое уходит на разбор в AI, другие форматы там не поддерживаются |
| **Отдельный лимит на аватар (2 МБ)** | Это картинка профиля, а не документ |
| **Имя файла в хранилище заменяется на GUID** | Исключает коллизии имён и path traversal (`../../etc/passwd`) |
| **Схема пути `users/{userId}/{module}/{guid}`** | Сразу видно, чьи файлы; легко вычистить всё при удалении аккаунта |
| **`StoragePath` хранится отдельно от `Url`** | Удалять объект нужно по внутреннему пути, а не по публичной ссылке. Наружу `StoragePath` не отдаётся вовсе |
| **Компенсация при сбое записи в БД** | Файл сначала уходит в хранилище, потом пишутся метаданные. Если запись упала — файл удаляется, иначе в bucket копились бы «сироты» |
| **Старый аватар удаляется ПОСЛЕ коммита** | Удали мы его раньше и откатись транзакция — профиль ссылался бы в пустоту |
| **Ошибка удаления из хранилища не роняет запрос** | Метаданные уже удалены, для пользователя операция успешна. Осиротевший объект логируется |
| **Проверка ссылок перед удалением → 409** | В БД у `Files` стоит `NoAction`: удаление используемого файла упало бы ошибкой внешнего ключа вместо понятного ответа |
| **`FileUploadData` вместо `IFormFile` в Application** | Слой не знает про ASP.NET — сервис можно вызвать из фонового задания или теста |
| **Файл копируется в `MemoryStream`** | Лимит 10 МБ делает это безопасным, а поток становится перечитываемым для проверки сигнатуры |
| **`MultipartBodyLengthLimit = 15 МБ`** | Срабатывает раньше валидации: сервер обрывает приём, не выделяя память под гигантский файл |
| **Локальные URL — относительные (`/uploads/...`)** | Абсолютный адрес хоста разный на localhost, preview и prod — пусть подставляет фронтенд |

---

## 5. Проверка (чек-лист)

Загрузка идёт как `multipart/form-data`, поэтому удобнее через **Swagger UI** — там появится кнопка выбора файла. Сценарии 31–41 в `LifeOS.API.http` содержат curl-эквиваленты.

- [ ] В логах при старте видно, какой провайдер активен
- [ ] `POST /api/files/upload?module=Study` с PDF → **201**, в ответе `url`
- [ ] Загрузка PNG в модуль Study → **400** `file.type_not_allowed`
- [ ] **Переименуй любой `.txt` в `.pdf` и загрузи** → **400** `file.signature_mismatch` ← *ключевая проверка безопасности*
- [ ] Файл больше 10 МБ → **400** `file.too_large`
- [ ] `GET /api/files?module=Study` возвращает только файлы этого модуля
- [ ] Чужой файл по Id → **404**
- [ ] `PUT /api/users/avatar` с PNG → **200** + `avatarUrl`
- [ ] `GET /api/auth/me` показывает новый `avatarUrl`
- [ ] Повторная загрузка аватара → в `/api/files?module=Avatar` осталась **одна** запись, старый файл исчез из хранилища
- [ ] `DELETE /api/files/{id}` → **204**, файл пропал и из БД, и из хранилища
- [ ] В ответе API **нет** поля `storagePath`

При локальном провайдере файлы физически лежат в `backend/src/LifeOS.API/bin/Debug/net8.0/wwwroot/uploads/users/{userId}/...` — можно открыть и убедиться.

---

## 6. Что дальше — Фаза 5 (AI Service)

1. Скелет FastAPI по структуре из `LifeOS_Architecture.md` §6
2. `security.py` — проверка internal API-key на каждый запрос от ASP.NET
3. Offline-обучение моделей на датасетах Kaggle (scikit-learn)
4. Роутеры инференса с возвратом `confidence` и `explanation`
5. `AiClient` на стороне ASP.NET — типизированный `HttpClient` с internal-key
