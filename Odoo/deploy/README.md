# HackPDM Production Server Deployment & Operations

This directory provides production-ready deployment, reverse-proxy TLS, backup/DR, log rotation, and health monitoring assets for the HackPDM Odoo 16.0 + PostgreSQL 15 backend.

## Architecture Requirements
- **TLS is mandatory:** `Src/HackPDM.Infrastructure/Odoo/OdooEndpoint.cs` blocks plain HTTP to any non-loopback host by default. Always terminate TLS 1.2+/1.3 at **Caddy** (`caddy/Caddyfile`) or **nginx** (`nginx/hackpdm.conf`) in front of Odoo on `127.0.0.1:8069`.
- **Production addon isolation:** Mount/install **only** `Odoo/addons/hackpdm` with `--without-demo=all`. Never install `Odoo/addons/hackpdm_test` in production.
- **Database + Filestore consistency:** Metadata lives in PostgreSQL while CAD binaries and preview images live in `data_dir/filestore/<db>`. Always back up and restore both together using `backup.sh` / `restore.sh`.

## Option A: Production Docker Compose (Recommended)
```bash
cd Odoo/deploy
cp .env.example .env
chmod 600 .env
# Edit .env: set HACKPDM_DOMAIN, ODOO_DB, and a strong random POSTGRES_PASSWORD

# 1. Start PostgreSQL and initialize the database with the production hackpdm addon
docker compose -f docker-compose.prod.yml up -d postgres
docker compose -f docker-compose.prod.yml run --rm odoo \
  odoo --db_host=postgres --db_user=odoo --db_password="$POSTGRES_PASSWORD" \
  -d "$ODOO_DB" -i hackpdm --without-demo=all --stop-after-init

# 2. Start Odoo + Caddy TLS reverse proxy
docker compose -f docker-compose.prod.yml up -d --build
```

## Option B: Bare-Metal Ubuntu/Debian (`Odoo/setup.sh`)
```bash
cd Odoo
ODOO_DB=hackpdm_prod ./setup.sh
sudo cp deploy/nginx/hackpdm.conf /etc/nginx/sites-available/hackpdm.conf
# Edit server_name and ssl_certificate paths, then enable and reload nginx
```

## Post-Install Security Hardening Checklist
1. **Rotate `admin` password** immediately and disable any unused default Odoo accounts.
2. **Assign RBAC groups** (`Viewer`, `Engineer`, `Administrator`) in Odoo Users -> HackPDM.
   - *Least-privilege option:* `hackpdm_groups.xml` assigns `group_pdm_user` (Engineer) to `base.default_user`; if your organization prefers read-only defaults for new accounts, demote `base.default_user` to `group_pdm_viewer`.
3. **Use Odoo API Keys:** Have engineers generate per-machine API keys in Odoo Preferences -> Account Security and enter the API key in HackPDM instead of their primary password.
4. **Revoke `CREATEDB` after DB initialization:**
   ```sql
   ALTER ROLE odoo NOCREATEDB;
   ```
5. **Host Firewall (`ufw`):**
   ```bash
   sudo ufw default deny incoming
   sudo ufw allow 22/tcp
   sudo ufw allow 80/tcp
   sudo ufw allow 443/tcp
   sudo ufw enable
   ```
   Ensure PostgreSQL (`5432`), Odoo (`8069`), and gevent (`8072`) are never exposed externally.

## Backups, Restore Drill & Monitoring
- **Daily consistent backup:** Schedule `backup.sh` via cron/systemd timer and replicate `BACKUP_ROOT` off-host.
- **Disaster Recovery drill:** Test `restore.sh <backup_dir> <staging_db>` on an isolated host and verify HackPDM `Get Latest` downloads files with matching SHA-1 checksums.
- **Health & TLS monitoring:** Schedule `HACKPDM_HOST=pdm.example.com ./healthcheck.sh` every 1–5 minutes.
