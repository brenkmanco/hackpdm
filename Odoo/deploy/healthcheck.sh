#!/usr/bin/env bash
# HackPDM production health, TLS, edge-hardening, and disk-space monitor.
# Intended for cron / systemd timer / monitoring agent execution.
#
# Usage:
#   HACKPDM_HOST=pdm.example.com DATA_DIR=/var/lib/odoo ./healthcheck.sh
set -euo pipefail

HOST="${HACKPDM_HOST:?Set HACKPDM_HOST (e.g. pdm.example.com)}"
DATA_DIR="${DATA_DIR:-/var/lib/odoo}"
CERT_MIN_DAYS="${CERT_MIN_DAYS:-14}"
DISK_WARN_PCT="${DISK_WARN_PCT:-75}"
DISK_CRIT_PCT="${DISK_CRIT_PCT:-85}"

STATUS=0

# 1. HTTPS /web/health check
HEALTH="$(curl -fsS --max-time 10 "https://${HOST}/web/health" || true)"
if [[ "$HEALTH" == *"pass"* ]]; then
    echo "[OK] https://${HOST}/web/health -> ${HEALTH}"
else
    echo "[CRIT] https://${HOST}/web/health failed (got: ${HEALTH:-empty})" >&2
    STATUS=2
fi

# 2. HTTP -> HTTPS redirect check
HTTP_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "http://${HOST}/web/health" || echo "000")"
if [[ "$HTTP_CODE" == "301" || "$HTTP_CODE" == "308" ]]; then
    echo "[OK] http://${HOST} redirects to HTTPS (${HTTP_CODE})"
else
    echo "[WARN] http://${HOST} returned ${HTTP_CODE} (expected 301/308 redirect)" >&2
    (( STATUS < 1 )) && STATUS=1
fi

# 3. /web/database/manager blocked check
DB_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://${HOST}/web/database/manager" || echo "000")"
if [[ "$DB_CODE" == "403" || "$DB_CODE" == "404" ]]; then
    echo "[OK] /web/database/manager blocked (${DB_CODE})"
else
    echo "[CRIT] /web/database/manager returned ${DB_CODE} (expected 403 or 404)" >&2
    STATUS=2
fi

# 4. TLS certificate expiry check
if command -v openssl >/dev/null 2>&1; then
    END_DATE="$(echo | openssl s_client -servername "$HOST" -connect "${HOST}:443" 2>/dev/null \
        | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2 || true)"
    if [[ -n "$END_DATE" ]]; then
        EXP_EPOCH="$(date -d "$END_DATE" +%s)"
        NOW_EPOCH="$(date +%s)"
        DAYS_LEFT="$(( (EXP_EPOCH - NOW_EPOCH) / 86400 ))"
        if (( DAYS_LEFT < CERT_MIN_DAYS )); then
            echo "[CRIT] TLS certificate for ${HOST} expires in ${DAYS_LEFT} days (${END_DATE})" >&2
            STATUS=2
        else
            echo "[OK] TLS certificate valid for ${DAYS_LEFT} days (expires ${END_DATE})"
        fi
    fi
fi

# 5. Data directory disk usage check
if [[ -d "$DATA_DIR" ]]; then
    PCT="$(df -P "$DATA_DIR" | awk 'NR==2 {gsub(/%/,"",$5); print $5}')"
    if (( PCT >= DISK_CRIT_PCT )); then
        echo "[CRIT] Disk usage on ${DATA_DIR} is ${PCT}% (>= ${DISK_CRIT_PCT}%)" >&2
        STATUS=2
    elif (( PCT >= DISK_WARN_PCT )); then
        echo "[WARN] Disk usage on ${DATA_DIR} is ${PCT}% (>= ${DISK_WARN_PCT}%)" >&2
        (( STATUS < 1 )) && STATUS=1
    else
        echo "[OK] Disk usage on ${DATA_DIR} is ${PCT}%"
    fi
fi

exit "$STATUS"
