#!/usr/bin/env bash
# Hardened bare-metal Odoo 16.0 + PostgreSQL + HackPDM production installer (Ubuntu/Debian).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OdooServiceUser="odoo"
OdooHome="/opt/odoo"
OdooVenv="/opt/odoo/venv"
OdooConfigDir="/etc/odoo"
OdooConfigFile="${OdooConfigDir}/odoo.conf"
OdooLogDir="/var/log/odoo"
FileStoreDir="${FILESTORE_DIR:-/var/lib/odoo}"
PsqlName="${PSQL_USER:-odoo}"
OdooDbName="${ODOO_DB:-hackpdm_prod}"

# Passwords come from the environment, or are generated securely; written only to /etc/odoo/odoo.conf (0600).
PsqlPass="${PSQL_PASS:-$(python3 -c 'import secrets; print(secrets.token_urlsafe(32))')}"
OdooMasterPass="${ODOO_MASTER_PASS:-$(python3 -c 'import secrets; print(secrets.token_urlsafe(32))')}"

# Worker sizing: 2 * CPU cores + 1, minimum 5 so large transfers never starve interactive browsing.
CPU_CORES="$(nproc 2>/dev/null || echo 2)"
DEFAULT_WORKERS="$(( CPU_CORES * 2 + 1 ))"
(( DEFAULT_WORKERS < 5 )) && DEFAULT_WORKERS=5
OdooWorkers="${ODOO_WORKERS:-$DEFAULT_WORKERS}"

# 1. System packages & PostgreSQL
sudo apt-get update
sudo apt-get install -y \
    git python3 python3-pip python3-venv build-essential wget curl \
    python3-dev libpq-dev libxml2-dev libxslt1-dev libldap2-dev libsasl2-dev libffi-dev \
    libmagic1 wkhtmltopdf postgresql logrotate

sudo systemctl enable --now postgresql

# 2. Dedicated non-root service user
if ! id -u "$OdooServiceUser" >/dev/null 2>&1; then
    sudo useradd --system --create-home --home-dir "$OdooHome" --shell /usr/sbin/nologin "$OdooServiceUser"
fi

# 3. PostgreSQL role (no SUPERUSER, no CREATEROLE; revoke CREATEDB after initializing the production DB)
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='${PsqlName}'" | grep -q 1; then
    sudo -u postgres createuser --createdb --no-superuser --no-createrole "$PsqlName"
fi
sudo -u postgres psql -v ON_ERROR_STOP=1 -c "ALTER USER \"${PsqlName}\" WITH PASSWORD '${PsqlPass}';"

# 4. Clone Odoo 16.0 and create isolated Python venv with pinned compatible dependencies
if [[ ! -d "${OdooHome}/src" ]]; then
    sudo -u "$OdooServiceUser" git clone --depth 1 --branch 16.0 https://github.com/odoo/odoo.git "${OdooHome}/src"
fi

if [[ ! -d "$OdooVenv" ]]; then
    sudo -u "$OdooServiceUser" python3 -m venv "$OdooVenv"
fi

sudo -u "$OdooServiceUser" "${OdooVenv}/bin/pip" install --upgrade pip "setuptools<80" wheel
sudo -u "$OdooServiceUser" "${OdooVenv}/bin/pip" install \
    "numpy<2.1" "sqlalchemy>=2,<3" python-magic webdavclient3
sudo -u "$OdooServiceUser" "${OdooVenv}/bin/pip" install -r "${OdooHome}/src/requirements.txt"

# 5. Directories & production odoo.conf (loopback-only, multi-worker, no demo, list_db=False)
sudo mkdir -p "$FileStoreDir" "$OdooConfigDir" "$OdooLogDir"
sudo chown -R "${OdooServiceUser}:${OdooServiceUser}" "$FileStoreDir" "$OdooLogDir"
sudo chmod 750 "$FileStoreDir" "$OdooLogDir"

cat <<EOF | sudo tee "$OdooConfigFile" >/dev/null
[options]
; ---- database ----
db_host = 127.0.0.1
db_port = 5432
db_user = ${PsqlName}
db_password = ${PsqlPass}
db_name = ${OdooDbName}
dbfilter = ^${OdooDbName}\$
list_db = False
without_demo = all
admin_passwd = ${OdooMasterPass}

; ---- network (behind TLS reverse proxy only) ----
http_interface = 127.0.0.1
http_port = 8069
gevent_port = 8072
proxy_mode = True

; ---- workers & resource limits ----
workers = ${OdooWorkers}
max_cron_threads = 1
limit_request = 8192
limit_time_cpu = 600
limit_time_real = 1200
limit_memory_soft = 2147483648
limit_memory_hard = 2684354560

; ---- files & logs ----
data_dir = ${FileStoreDir}
addons_path = ${OdooHome}/src/addons,${SCRIPT_DIR}/addons
logfile = ${OdooLogDir}/odoo.log
log_level = info
EOF

sudo chown "${OdooServiceUser}:${OdooServiceUser}" "$OdooConfigFile"
sudo chmod 600 "$OdooConfigFile"

# 6. Log rotation
if [[ -f "${SCRIPT_DIR}/deploy/logrotate-odoo" ]]; then
    sudo install -m 644 "${SCRIPT_DIR}/deploy/logrotate-odoo" /etc/logrotate.d/odoo
fi

# 7. Hardened systemd unit
cat <<EOF | sudo tee /etc/systemd/system/odoo.service >/dev/null
[Unit]
Description=HackPDM Odoo 16.0 Service
After=network-online.target postgresql.service
Wants=network-online.target

[Service]
Type=simple
User=${OdooServiceUser}
Group=${OdooServiceUser}
ExecStart=${OdooVenv}/bin/python3 ${OdooHome}/src/odoo-bin -c ${OdooConfigFile}
Restart=on-failure
RestartSec=5s
LimitNOFILE=65536
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now odoo

echo "Odoo 16.0 service enabled and started (config: ${OdooConfigFile}, workers: ${OdooWorkers})."
echo "Next steps: configure nginx/Caddy TLS reverse proxy (see Odoo/deploy/README.md) and initialize '${OdooDbName}' with '-i hackpdm --without-demo=all'."
