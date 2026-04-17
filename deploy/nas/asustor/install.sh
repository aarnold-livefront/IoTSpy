#!/bin/sh
# install.sh — first-time IoTSpy setup on Asustor NAS
# Called by ADM on initial install only (not on upgrades).
# Must be /bin/sh compatible — Asustor ADM uses busybox sh.

set -e

INSTALL_DIR="/volume1/IoTSpy"
ENV_FILE="${INSTALL_DIR}/.env"

# Determine compose command (v2 plugin preferred, v1 fallback)
if docker compose version >/dev/null 2>&1; then
    COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE="docker-compose"
else
    echo "ERROR: Docker Compose not found. Install the Docker app from ADM App Central first." >&2
    exit 1
fi

# Create data directories with restrictive permissions
mkdir -p \
    "${INSTALL_DIR}/data" \
    "${INSTALL_DIR}/logs" \
    "${INSTALL_DIR}/plugins"
chmod 700 "${INSTALL_DIR}/data"

# Copy compose file (overwrite on upgrade to pick up changes)
cp "$(dirname "$0")/conf/docker-compose.yml" "${INSTALL_DIR}/docker-compose.yml"

# Generate .env from template only on first install (preserve existing secrets on upgrade)
if [ ! -f "${ENV_FILE}" ]; then
    cp "$(dirname "$0")/conf/.env.template" "${ENV_FILE}"

    # Generate a cryptographically random 64-char hex JWT secret
    if command -v openssl >/dev/null 2>&1; then
        JWT_SECRET="$(openssl rand -hex 32)"
    else
        JWT_SECRET="$(cat /dev/urandom | od -An -tx1 | tr -d ' \n' | head -c 64)"
    fi

    sed -i "s/REPLACE_WITH_GENERATED_SECRET/${JWT_SECRET}/" "${ENV_FILE}"
    echo "Generated JWT secret and saved to ${ENV_FILE}"
fi

# Pull the Docker image in the background (start.sh will wait for it if needed)
$COMPOSE -f "${INSTALL_DIR}/docker-compose.yml" --env-file "${ENV_FILE}" pull || true

echo "IoTSpy installed. Open http://$(hostname -i 2>/dev/null || echo 'NAS-IP'):5000 to get started."
exit 0
