#!/bin/sh
# pre-install.sh — verify prerequisites before ADM commits the install.
# Must be /bin/sh compatible — Asustor ADM uses busybox sh.

set -e

if docker compose version >/dev/null 2>&1; then
    exit 0
elif command -v docker-compose >/dev/null 2>&1; then
    exit 0
else
    echo "ERROR: Docker Compose not found. Install the Docker app from ADM App Central first." >&2
    exit 1
fi
