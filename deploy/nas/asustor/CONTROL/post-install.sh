#!/bin/sh
# post-install.sh — first-time IoTSpy setup on Asustor NAS.
# Called by ADM on both initial install and upgrade ($APKG_PKG_STATUS
# distinguishes the two). Must be /bin/sh compatible — ADM uses busybox sh.

set -e

INSTALL_DIR="${APKG_PKG_DIR}"
ENV_FILE="${INSTALL_DIR}/.env"

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

# Copy compose file (overwrite on upgrade to pick up changes), then point its
# bind mounts at the actual install directory ADM assigned (may not be
# /volume1/IoTSpy if the user installs to a different volume).
cp "${INSTALL_DIR}/conf/docker-compose.yml" "${INSTALL_DIR}/docker-compose.yml"
sed -i "s|/volume1/IoTSpy|${INSTALL_DIR}|g" "${INSTALL_DIR}/docker-compose.yml"

# Generate .env from template only on first install (preserve existing secrets on upgrade)
if [ ! -f "${ENV_FILE}" ]; then
    cp "${INSTALL_DIR}/conf/.env.template" "${ENV_FILE}"

    # Generate a cryptographically random 64-char hex JWT secret
    if command -v openssl >/dev/null 2>&1; then
        JWT_SECRET="$(openssl rand -hex 32)"
    else
        JWT_SECRET="$(cat /dev/urandom | od -An -tx1 | tr -d ' \n' | head -c 64)"
    fi

    sed -i "s/REPLACE_WITH_GENERATED_SECRET/${JWT_SECRET}/" "${ENV_FILE}"
    echo "Generated JWT secret and saved to ${ENV_FILE}"
fi

# Pull the Docker image in the background (start-stop.sh will wait for it if needed)
$COMPOSE -f "${INSTALL_DIR}/docker-compose.yml" --env-file "${ENV_FILE}" pull || true

echo "IoTSpy installed. Open http://$(hostname -i 2>/dev/null || echo 'NAS-IP'):5000 to get started."
exit 0
