# Ubuntu 22.04: сервер LumaDesk по публичному IP, без домена

Этот вариант добавлен для VPS с постоянным публичным IPv4 и без Nginx.
Используется Caddy 2.11.9 и публичный сертификат Let's Encrypt для IP
с профилем `shortlived`. Самоподписанный сертификат не используется,
проверка сертификатов в Windows-клиенте не отключается.

Конфигурация подготовлена; публичный сервер и сертификат ещё не созданы.
Нужен реальный внешний IPv4 вашего VPS. Адрес 203.0.113.10 в примере
предназначен только для документации и работать не будет.

## 1. Подготовка

На Ubuntu 22.04 установите Docker Engine и плагин Docker Compose по инструкции:
https://docs.docker.com/engine/install/ubuntu/

Убедитесь, что TCP-порты 80 и 443 свободны и доступны извне как в сетевом
экране VPS, так и у хостинг-провайдера. SSH-порт должен оставаться доступным.
Эта инструкция не меняет правила сетевого экрана и не включает UFW автоматически.
Перенесите папку `Server` из архива в выбранный каталог на VPS.

## 2. Настройка и запуск

В папке Server:

```bash
cp .env.ip.example .env.ip
nano .env.ip
```

Замените `SERVER_IP` на реальный публичный IPv4. Без `https://` и без порта.
Домен и почтовый адрес для этого варианта не требуются.
После редактирования:

```bash
docker compose --env-file .env.ip -f compose.ip.yaml up -d --build
docker compose --env-file .env.ip -f compose.ip.yaml logs --tail=80 caddy api
```

Не запускайте одновременно `compose.yaml`, `compose.existing-proxy.yaml`
и этот вариант: это альтернативные способы запуска одного проекта.
Если уже запускали основной вариант, сначала остановите его обычной командой
`docker compose down` БЕЗ `-v`, затем запускайте IP-вариант из той же папки.
Общие имена томов сохраняют базу при переключении в рамках одного compose-проекта.

Caddy запросит публичный сертификат и будет обновлять его автоматически.
Сертификат IP действует около шести дней, поэтому VPS и сервис Caddy должны
оставаться включёнными. Не удаляйте его том `caddy_data` и не используйте `down -v`.
При смене IP обновите `.env.ip` и перезапустите compose, затем адрес в клиенте.

## 3. Проверка

Откройте в браузере:

```text
https://РЕАЛЬНЫЙ_IP/health
```

Должен открыться HTTPS без предупреждения о сертификате и ответ:

```json
{"status":"ok","version":"2.1"}
```

Затем в LumaDesk → трей → «Аккаунт и синхронизация» введите
`https://РЕАЛЬНЫЙ_IP` без `/health`, зарегистрируйтесь и войдите на втором ПК.
Клиент версии 2.1 из архива уже поддерживает HTTPS по IP; отдельная
переустановка .NET или отключение проверки TLS не требуются.

Если сертификат пока не получен, проверьте журналы Caddy, внешний IP и доступность
80/443. Не переходите на обычный удалённый HTTP и не нажимайте обход предупреждения:
клиент всё равно должен проверять нормальный доверенный сертификат.
Фактический выпуск зависит от доступности VPS извне и политики центра сертификации.

## 4. Обслуживание

Для IP-варианта к каждой команде compose добавляйте эти параметры:

```bash
docker compose --env-file .env.ip -f compose.ip.yaml ps
docker compose --env-file .env.ip -f compose.ip.yaml exec api python manage.py backup /data/backups/manual.sqlite3
docker compose --env-file .env.ip -f compose.ip.yaml cp api:/data/backups/manual.sqlite3 ./lumadesk-backup.sqlite3
docker compose --env-file .env.ip -f compose.ip.yaml exec api python manage.py reset-password USERNAME
```

Остальная информация о данных, аккаунтах, паролях и синхронизации — в README.md.

Основания конфигурации:
- https://letsencrypt.org/2026/01/15/6day-and-ip-general-availability
- https://caddyserver.com/docs/caddyfile/directives/tls (ACME profile)
- https://caddyserver.com/docs/caddyfile/options#default-sni (клиенты без SNI)
- https://github.com/caddyserver/caddy/releases/tag/v2.11.9

## Установщик для собственного VPS (Ubuntu 22.04)

Из полной папки Server выполните от root:

```bash
bash install-ubuntu22-ip.sh YOUR_SERVER_IP
```

Замените `YOUR_SERVER_IP` на публичный IPv4 вашего VPS. Адрес по умолчанию
не задан; без аргумента установка не начинается. Скрипт устанавливает Docker из официального apt-репозитория,
если Docker отсутствует; существующую установку не заменяет. Конфликтующие
пакеты и службы не удаляются. API и Caddy запускаются проектом `lumadesk`.
Если UFW уже включён, добавляются разрешения TCP 80/443; включения или сброса
UFW нет. Внешний firewall хостера может потребовать ручного открытия портов.

Для обслуживания именно установленного этим скриптом экземпляра:

```bash
docker compose -p lumadesk --env-file .env.ip -f compose.ip.yaml ps
docker compose -p lumadesk --env-file .env.ip -f compose.ip.yaml logs --tail=80 caddy api
docker compose -p lumadesk --env-file .env.ip -f compose.ip.yaml exec api python manage.py backup /data/backups/manual.sqlite3
```

Том данных: `lumadesk_app_data`. Скрипт не переносит автоматически данные
из ранее запущенного проекта с другим именем. Если 80/443 заняты чужим
сервисом, он останавливается с сообщением, не выключая этот сервис.
Синтаксис установщика и шаблон конфигурации Caddy проверены; реальная
установка и выдача сертификата на вашем VPS ещё не выполнялись.
