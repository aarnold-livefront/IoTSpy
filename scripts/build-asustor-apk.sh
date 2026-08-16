#!/bin/sh
# build-asustor-apk.sh — Build Asustor APKG 2.0 packages for IoTSpy.
#
# Usage:
#   sh scripts/build-asustor-apk.sh
#
# Environment variables:
#   VERSION     Package version string (default: git tag without leading 'v')
#   IMAGE_TAG   Docker image tag to embed in bundled compose (default: VERSION)
#   ARCH        arm64 | x86-64 | both  (default: both)
#
# Output:
#   dist/iotspy_<VERSION>_arm64.apk
#   dist/iotspy_<VERSION>_x86-64.apk
#
# An APKG 2.0 .apk is a ZIP archive containing exactly three entries:
#   apkg-version   — plain text version string
#   control.tar.gz — tar.gz of deploy/nas/asustor/CONTROL/ (metadata + lifecycle scripts)
#   data.tar.gz    — tar.gz of the app payload (bundled docker-compose.yml, etc.)
# See https://downloadgb.asustor.com/developer/ for the App Central Developer Guide.

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="${REPO_ROOT}/deploy/nas/asustor"
DIST_DIR="${REPO_ROOT}/dist"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required to assemble the .apk zip container." >&2
    exit 1
fi

# Resolve version from git tag if not provided
if [ -z "${VERSION}" ]; then
    VERSION="$(git -C "${REPO_ROOT}" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo '0.0.0')"
fi
IMAGE_TAG="${IMAGE_TAG:-${VERSION}}"
ARCH="${ARCH:-both}"

mkdir -p "${DIST_DIR}"

build_apk() {
    TARGET_ARCH="$1"   # arm64 or x86-64
    WORK_DIR="$(mktemp -d)"
    # shellcheck disable=SC2064
    trap "rm -rf '${WORK_DIR}'" EXIT INT TERM

    echo "Building iotspy_${VERSION}_${TARGET_ARCH}.apk ..."

    mkdir -p "${WORK_DIR}/CONTROL" "${WORK_DIR}/data"

    # ── CONTROL: metadata + lifecycle scripts ──────────────────────────────
    cp -r "${SRC_DIR}/CONTROL/." "${WORK_DIR}/CONTROL/"
    sed -e "s/IOTSPY_VERSION/${VERSION}/g" -e "s/IOTSPY_ARCH/${TARGET_ARCH}/g" \
        "${SRC_DIR}/CONTROL/config.json.template" > "${WORK_DIR}/CONTROL/config.json"
    rm "${WORK_DIR}/CONTROL/config.json.template"
    chmod +x "${WORK_DIR}"/CONTROL/*.sh

    # ── data: app payload ───────────────────────────────────────────────────
    cp -r "${SRC_DIR}/data/." "${WORK_DIR}/data/"
    cp "${REPO_ROOT}/docker-compose.nas.yml" "${WORK_DIR}/data/conf/docker-compose.yml"
    # Pin the image tag so installs use the versioned image
    sed -i "s|iotspy:\${IOTSPY_VERSION:-latest}|iotspy:${IMAGE_TAG}|g" \
        "${WORK_DIR}/data/conf/docker-compose.yml"

    # ── assemble the three archive members ──────────────────────────────────
    # apkg-version is the APKG *format* version (always "2.0"), NOT the app
    # version — ADM reads this to pick a format-1.0-vs-2.0 parser and rejects
    # the archive as invalid if it's anything else. App version lives in
    # config.json's general.version field.
    printf '2.0\n' > "${WORK_DIR}/apkg-version"
    tar -czf "${WORK_DIR}/control.tar.gz" -C "${WORK_DIR}/CONTROL" .
    tar -czf "${WORK_DIR}/data.tar.gz" -C "${WORK_DIR}/data" .

    OUTPUT="${DIST_DIR}/iotspy_${VERSION}_${TARGET_ARCH}.apk"
    rm -f "${OUTPUT}"
    python3 -c "
import zipfile, sys
out, work = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for name in ('apkg-version', 'control.tar.gz', 'data.tar.gz'):
        z.write(work + '/' + name, name)
" "${OUTPUT}" "${WORK_DIR}"

    echo "Contents of ${OUTPUT}:"
    python3 -c "
import zipfile, sys
with zipfile.ZipFile(sys.argv[1]) as z:
    for n in z.namelist():
        print(' ', n)
" "${OUTPUT}"

    echo "  → ${OUTPUT}"
    trap - EXIT INT TERM
    rm -rf "${WORK_DIR}"
}

case "${ARCH}" in
    arm64)   build_apk arm64 ;;
    x86-64)  build_apk x86-64 ;;
    both)
        build_apk arm64
        build_apk x86-64
        ;;
    *)
        echo "Unknown ARCH '${ARCH}'. Valid values: arm64, x86-64, both" >&2
        exit 1
        ;;
esac

echo "Done. APK(s) written to ${DIST_DIR}/"
