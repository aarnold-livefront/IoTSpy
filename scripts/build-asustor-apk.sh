#!/bin/sh
# build-asustor-apk.sh — Build Asustor APK packages for IoTSpy.
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

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="${REPO_ROOT}/deploy/nas/asustor"
DIST_DIR="${REPO_ROOT}/dist"

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

    # Copy all APK source files
    cp -r "${SRC_DIR}/." "${WORK_DIR}/"

    # Patch version and architecture placeholders in apkg.info
    sed -i "s/IOTSPY_VERSION/${VERSION}/g" "${WORK_DIR}/apkg.info"
    sed -i "s/IOTSPY_ARCH/${TARGET_ARCH}/g" "${WORK_DIR}/apkg.info"

    # Bundle the NAS compose file into conf/
    mkdir -p "${WORK_DIR}/conf"
    cp "${REPO_ROOT}/docker-compose.nas.yml" "${WORK_DIR}/conf/docker-compose.yml"

    # Pin the image tag in the bundled compose so installs use the versioned image
    sed -i "s|iotspy:\${IOTSPY_VERSION:-latest}|iotspy:${IMAGE_TAG}|g" \
        "${WORK_DIR}/conf/docker-compose.yml"

    # Ensure all lifecycle scripts are executable inside the archive
    chmod +x \
        "${WORK_DIR}/install.sh" \
        "${WORK_DIR}/start.sh" \
        "${WORK_DIR}/stop.sh" \
        "${WORK_DIR}/remove.sh" \
        "${WORK_DIR}/webman/3rdparty/IoTSpy/index.cgi"

    OUTPUT="${DIST_DIR}/iotspy_${VERSION}_${TARGET_ARCH}.apk"
    tar -czf "${OUTPUT}" -C "${WORK_DIR}" .

    # Verify the archive lists the expected top-level files
    echo "Contents of ${OUTPUT}:"
    tar -tzvf "${OUTPUT}" | grep -E '^\-|^\./' | head -20

    echo "  → ${OUTPUT}"
    # Reset trap to avoid double-remove on subsequent iterations
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
