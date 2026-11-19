#!/usr/bin/env bash
# Verified HackPDM Disaster Recovery restore script.
# Verifies SHA256SUMS, restores PostgreSQL custom-format dump, and restores the matching Odoo filestore.
#
# Usage (bare-metal):
#   ./restore.sh /var/backups/hackpdm/hackpdm_prod-20261118T000000Z hackpdm_restore
#
# Usage (Docker Compose):
#   DOCKER_PG=hackpdm-prod-pg DOCKER_ODOO=hackpdm-prod-odoo \
#     ./restore.sh /var/backups/hackpdm/hackpdm_prod-20261118T000000Z hackpdm_restore
set -euo pipefail

BACKUP_DIR="${1:?Usage: restore.sh <backup_dir> <target_db>}"
TARGET_DB="${2:?Usage: restore.sh <backup_dir> <target_db>}"
PG_USER="${PG_USER:-odoo}"
DATA_DIR="${DATA_DIR:-/var/lib/odoo}"
DOCKER_PG="${DOCKER_PG:-}"
DOCKER_ODOO="${DOCKER_ODOO:-}"

if [[ ! -f "${BACKUP_DIR}/SHA256SUMS" ]]; then
    echo "[restore] ERROR: SHA256SUMS missing in ${BACKUP_DIR}" >&2
    exit 1
fi

echo "[restore] Verifying backup checksums..."
(cd "$BACKUP_DIR" && sha256sum -c SHA256SUMS)

echo "[restore] Creating and restoring PostgreSQL database '${TARGET_DB}'..."
if [[ -n "$DOCKER_PG" ]]; then
    docker exec "$DOCKER_PG" psql -U "$PG_USER" -d postgres -c "CREATE DATABASE \"${TARGET_DB}\" OWNER \"${PG_USER}\";"
    docker exec -i "$DOCKER_PG" pg_restore -U "$PG_USER" -d "$TARGET_DB" --no-owner --role="$PG_USER" < "${BACKUP_DIR}/database.dump"
else
    createdb -U "$PG_USER" -O "$PG_USER" "$TARGET_DB"
    pg_restore -U "$PG_USER" -d "$TARGET_DB" --no-owner --role="$PG_USER" "${BACKUP_DIR}/database.dump"
fi

echo "[restore] Restoring filestore into '${DATA_DIR}/filestore/${TARGET_DB}'..."
TMP_EXTRACT="$(mktemp -d)"
trap 'rm -rf "$TMP_EXTRACT"' EXIT
tar -xzf "${BACKUP_DIR}/filestore.tar.gz" -C "$TMP_EXTRACT"
SRC_FS="$(find "$TMP_EXTRACT" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
if [[ -z "$SRC_FS" ]]; then
    echo "[restore] ERROR: No filestore directory found inside filestore.tar.gz" >&2
    exit 1
fi

if [[ -n "$DOCKER_ODOO" ]]; then
    docker exec "$DOCKER_ODOO" mkdir -p "${DATA_DIR}/filestore/${TARGET_DB}"
    tar -C "$SRC_FS" -cf - . | docker exec -i "$DOCKER_ODOO" tar -C "${DATA_DIR}/filestore/${TARGET_DB}" -xf -
else
    mkdir -p "${DATA_DIR}/filestore/${TARGET_DB}"
    cp -a "${SRC_FS}/." "${DATA_DIR}/filestore/${TARGET_DB}/"
fi

echo "[restore] Restore complete for '${TARGET_DB}'. Start Odoo with --database=${TARGET_DB} and verify Get Latest SHA-1 integrity."
