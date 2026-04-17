#!/bin/sh
# stop.sh — stop IoTSpy containers without removing them
# Called by ADM when the app is disabled.

INSTALL_DIR="/volume1/IoTSpy"

if docker compose version >/dev/null 2>&1; then
    COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE="docker-compose"
else
    exit 0
fi

$COMPOSE \
    -f "${INSTALL_DIR}/docker-compose.yml" \
    --env-file "${INSTALL_DIR}/.env" \
    stop

exit 0
