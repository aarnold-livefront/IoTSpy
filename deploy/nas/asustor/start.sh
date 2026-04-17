#!/bin/sh
# start.sh — start IoTSpy containers
# Called by ADM when the app is enabled or after NAS boot.

INSTALL_DIR="/volume1/IoTSpy"

if docker compose version >/dev/null 2>&1; then
    COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE="docker-compose"
else
    echo "ERROR: Docker Compose not found." >&2
    exit 1
fi

$COMPOSE \
    -f "${INSTALL_DIR}/docker-compose.yml" \
    --env-file "${INSTALL_DIR}/.env" \
    up -d --remove-orphans

exit 0
