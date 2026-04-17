#!/bin/sh
# remove.sh — uninstall IoTSpy
# Called by ADM on uninstall. Removes containers and image.
# USER DATA in /volume1/IoTSpy/data is intentionally preserved.

INSTALL_DIR="/volume1/IoTSpy"

if docker compose version >/dev/null 2>&1; then
    COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE="docker-compose"
else
    COMPOSE=""
fi

# Stop and remove containers + networks
if [ -n "$COMPOSE" ] && [ -f "${INSTALL_DIR}/docker-compose.yml" ]; then
    $COMPOSE \
        -f "${INSTALL_DIR}/docker-compose.yml" \
        --env-file "${INSTALL_DIR}/.env" \
        down --remove-orphans 2>/dev/null || true
fi

# Remove the image to reclaim disk space
docker image rm ghcr.io/aarnold-livefront/iotspy 2>/dev/null || true

# Remove app files but preserve user data
rm -f "${INSTALL_DIR}/docker-compose.yml"
# .env is kept so secrets survive a reinstall

echo "IoTSpy removed. Your data is preserved in ${INSTALL_DIR}/data"
exit 0
