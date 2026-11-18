#!/usr/bin/env bash
# LumaDesk: Ubuntu 22.04, public IPv4 HTTPS, no domain or Nginx required.
set -Eeuo pipefail
umask 077
TARGET_IP="${1:-}"
SERVER_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
fail() { printf '\nОшибка: %s\n' "$*" >&2; exit 1; }
trap 'printf "\nУстановка остановлена (строка %s). Исправь ошибку выше; существующие тома не удаляются.\n" "$LINENO" >&2' ERR
[[ -n "$TARGET_IP" ]] || fail 'Укажите публичный IPv4: sudo bash install-ubuntu22-ip.sh YOUR_SERVER_IP'
[[ "$EUID" -eq 0 ]] || fail 'Запусти установщик от root: sudo bash install-ubuntu22-ip.sh YOUR_SERVER_IP'
[[ "$TARGET_IP" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]] || fail 'Нужен IPv4 без https:// и порта.'
IFS='.' read -r -a ip_octets <<< "$TARGET_IP"
for octet in "${ip_octets[@]}"; do
    [[ "$octet" == 0 || "$octet" != 0* ]] || fail 'Не используй ведущие нули в IPv4.'
    (( 10#$octet <= 255 )) || fail 'Некорректный IPv4.'
done
[[ -r /etc/os-release ]] || fail 'Не удалось определить ОС.'
. /etc/os-release
[[ "${ID:-}" == ubuntu && "${VERSION_ID:-}" == 22.04 ]] || fail 'Этот установщик предназначен для Ubuntu 22.04.'
[[ -f "$SERVER_DIR/compose.ip.yaml" && -f "$SERVER_DIR/Caddyfile.ip" && -f "$SERVER_DIR/app.py" ]] || fail 'Запусти файл из полной папки Server, распакованной из архива.'

printf 'LumaDesk: установка для https://%s\n' "$TARGET_IP"

# Do not stop or replace unrelated services that already own the public ports.
if command -v ss >/dev/null 2>&1; then
    port_listeners="$(ss -H -ltn '( sport = :80 or sport = :443 )' || true)"
    own_caddy=''
    if command -v docker >/dev/null 2>&1; then
        own_caddy="$(docker ps -q --filter label=com.docker.compose.project=lumadesk --filter label=com.docker.compose.service=caddy 2>/dev/null || true)"
    fi
    if [[ -n "$port_listeners" && -z "$own_caddy" ]]; then
        printf '%s\n' "$port_listeners" >&2
        fail 'Порт 80 или 443 уже занят. Установщик не останавливает чужие службы.'
    fi
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends ca-certificates curl

if command -v docker >/dev/null 2>&1; then
    printf 'Docker уже установлен; существующий Docker не переустанавливается.\n'
    if ! docker compose version >/dev/null 2>&1; then
        if dpkg-query -W -f='${Status}' docker-ce 2>/dev/null | grep -q 'install ok installed'; then
            apt-get install -y --no-remove docker-compose-plugin
        else
            fail 'Docker установлен без Compose v2. Добавь совместимый docker-compose-plugin для своей установки и повтори запуск.'
        fi
    fi
else
    conflicts=()
    for package in docker.io docker-compose docker-compose-v2 docker-doc docker-buildx podman-docker containerd runc; do
        if dpkg-query -W -f='${Status}' "$package" 2>/dev/null | grep -q 'install ok installed'; then
            conflicts+=("$package")
        fi
    done
    if (( ${#conflicts[@]} > 0 )); then
        fail "Обнаружены конфликтующие пакеты: ${conflicts[*]}. Автоматически удалять их установщик не будет."
    fi
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    docker_arch="$(dpkg --print-architecture)"
    cat > /etc/apt/sources.list.d/docker.sources <<REPOSITORY
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: jammy
Components: stable
Architectures: $docker_arch
Signed-By: /etc/apt/keyrings/docker.asc
REPOSITORY
    apt-get update
    apt-get install -y --no-remove docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi

systemctl enable --now docker
docker info >/dev/null
docker compose version

# Docker publishing is checked in addition to ss because docker-proxy can be disabled.
for public_port in 80 443; do
    while IFS= read -r container_id; do
        [[ -n "$container_id" ]] || continue
        owner="$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$container_id")"
        [[ "$owner" == lumadesk ]] || fail "Порт $public_port публикует другой контейнер. Он не будет остановлен."
    done < <(docker ps -q --filter "publish=$public_port")
done

cd -- "$SERVER_DIR"
if [[ ! -f .env.ip ]]; then
    cp .env.ip.example .env.ip
fi
if grep -q '^SERVER_IP=' .env.ip; then
    sed -i "s/^SERVER_IP=.*/SERVER_IP=$TARGET_IP/" .env.ip
else
    printf '\nSERVER_IP=%s\n' "$TARGET_IP" >> .env.ip
fi
chmod 600 .env.ip

# Preserve existing SSH/firewall policy; never enable or reset UFW automatically.
if command -v ufw >/dev/null 2>&1 && LC_ALL=C ufw status | grep -q '^Status: active'; then
    ufw allow 80/tcp
    ufw allow 443/tcp
fi

compose() { docker compose -p lumadesk --env-file "$SERVER_DIR/.env.ip" -f "$SERVER_DIR/compose.ip.yaml" "$@"; }
compose config --quiet
compose up -d --build
printf '\nОжидаем публичный сертификат и готовность API (до нескольких минут)…\n'
ready=0
for attempt in $(seq 1 48); do
    # The hostname remains the public IP, so normal certificate checks still apply.
    # Loopback routing avoids NAT hairpin problems on the VPS itself.
    if health="$(curl --noproxy '*' --resolve "$TARGET_IP:443:127.0.0.1" --fail --silent --show-error --max-time 5 "https://$TARGET_IP/health" 2>/dev/null)"; then
        if [[ "$health" == *'"status":"ok"'* || "$health" == *'"status": "ok"'* ]]; then
            ready=1
            break
        fi
    fi
    printf '.'
    sleep 5
done
printf '\n'
if (( ready == 0 )); then
    compose logs --tail=60 caddy api
    fail 'HTTPS ещё не готов. Проверь внешний firewall хостера, доступность 80/443 и сообщения Caddy. Повторный запуск не удаляет данные.'
fi
printf '\nЛокальная HTTPS-проверка пройдена: %s\n' "$health"
printf 'Открой с компьютера: https://%s/health\n' "$TARGET_IP"
printf 'Адрес в LumaDesk:   https://%s\n' "$TARGET_IP"
printf 'В трее: Аккаунт и синхронизация → Регистрация.\n'
printf '\nПроект Docker Compose: lumadesk. Данные хранятся в томе lumadesk_app_data.\n'
