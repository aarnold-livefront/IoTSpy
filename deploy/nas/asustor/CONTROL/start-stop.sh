#!/bin/sh
# start-stop.sh — start/stop IoTSpy containers.
# Called by ADM as `start-stop.sh start` when the app is enabled or after NAS
# boot, and `start-stop.sh stop` when the app is disabled.

INSTALL_DIR="${APKG_PKG_DIR}"

if docker compose version >/dev/null 2>&1; then
    COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE="docker-compose"
else
    echo "ERROR: Docker Compose not found." >&2
    exit 1
fi

case "$1" in

    start)
        $COMPOSE \
            -f "${INSTALL_DIR}/docker-compose.yml" \
            --env-file "${INSTALL_DIR}/.env" \
            up -d --remove-orphans
        ;;

    stop)
        $COMPOSE \
            -f "${INSTALL_DIR}/docker-compose.yml" \
            --env-file "${INSTALL_DIR}/.env" \
            stop
        ;;

    *)
        echo "usage: $0 {start|stop}" >&2
        exit 1
        ;;

esac

exit 0
