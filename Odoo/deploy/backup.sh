#!/usr/bin/env bash
# Consistent HackPDM backup: captures BOTH PostgreSQL (pg_dump -Fc) and Odoo's filestore together
# with a SHA-256 manifest, then prunes backups older than RETENTION_DAYS.
#
# Usage (bare-metal):
#   ODOO_DB=hackpdm_prod DATA_DIR=/var/lib/odoo BACKUP_ROOT=/var/backups/hackpdm ./backup.sh
#
# Usage (Docker Compose):
#   DOCKER_PG=hackpdm-prod-pg DOCKER_ODOO=hackpdm-prod-odoo ODOO_DB=hackpdm_prod \
#     BACKUP_ROOT=/var/backups/hackpdm ./backup.sh
set -euo pipefail

ODOO_DB="${ODOO_DB:?Set ODOO_DB to the target Odoo database name}"
PG_USER="${PG_USER:-odoo}"
DATA_DIR="${DATA_DIR:-/var/lib/odoo}"
BACKUP_ROOT="${BACKUP_ROOT:-/var/backups/hackpdm}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
DOCKER_PG="${DOCKER_PG:-}"
DOCKER_ODOO="${DOCKER_ODOO:-}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="${BACKUP_ROOT}/${ODOO_DB}-${STAMP}"
mkdir -p "$DEST"
chmod 700 "$DEST"

echo "[backup] Starting consistent backup for '${ODOO_DB}' -> ${DEST}"

# 1. PostgreSQL custom-format dump
if [[ -n "$DOCKER_PG" ]]; then
    docker exec "$DOCKER_PG" pg_dump -U "$PG_USER" -Fc --no-owner "$ODOO_DB" > "${DEST}/database.dump"
else
    pg_dump -U "$PG_USER" -Fc --no-owner "$ODOO_DB" > "${DEST}/database.dump"
fi

# 2. Odoo filestore archive (contains immutable CAD binary attachments & preview images)
if [[ -n "$DOCKER_ODOO" ]]; then
    docker exec "$DOCKER_ODOO" tar -C "${DATA_DIR}/filestore" -czf - "$ODOO_DB" > "${DEST}/filestore.tar.gz"
else
    if [[ ! -d "${DATA_DIR}/filestore/${ODOO_DB}" ]]; then
        echo "[backup] ERROR: filestore directory not found: ${DATA_DIR}/filestore/${ODOO_DB}" >&2
        exit 1
    fi
    tar -C "${DATA_DIR}/filestore" -czf "${DEST}/filestore.tar.gz" "$ODOO_DB"
fi

# 3. Integrity manifest
(cd "$DEST" && sha256sum database.dump filestore.tar.gz > SHA256SUMS)
chmod 600 "${DEST}/database.dump" "${DEST}/filestore.tar.gz" "${DEST}/SHA256SUMS"

# 4. Retention pruning
if [[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]] && (( RETENTION_DAYS > 0 )); then
    find "$BACKUP_ROOT" -maxdepth 1 -mindepth 1 -type d -name "${ODOO_DB}-*" -mtime +"$RETENTION_DAYS" -exec rm -rf {} +
fi

echo "[backup] Completed successfully: ${DEST}"
cat "${DEST}/SHA256SUMS"
