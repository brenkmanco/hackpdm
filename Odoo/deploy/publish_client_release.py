#!/usr/bin/env python3
"""Publishes a new HackPDM client version to Odoo's hp.settings records over XML-RPC.

Updates:
  - client_version
  - client_download_url
  - client_download_sha256

Usage:
  ODOO_URL=https://pdm.example.com ODOO_DB=hackpdm_prod ODOO_USER=admin ODOO_PASSWORD=... \
    ./publish_client_release.py 1.2.0 https://github.com/.../HackPDM-1.2.0-win-x64-setup.exe <sha256>
"""
import os
import re
import sys
import xmlrpc.client


def main() -> int:
    if len(sys.argv) != 4:
        print("Usage: publish_client_release.py <version> <download_url> <sha256>", file=sys.stderr)
        return 2

    version, download_url, sha256 = sys.argv[1].strip(), sys.argv[2].strip(), sys.argv[3].strip().lower()
    if not version or not re.fullmatch(r"[0-9.<>=,\s\-;]+", version):
        sys.exit(f"Invalid version or range '{version}' (e.g. '1.2.0', '>=1.2.0, <2.0.0', or '1.1.0 - 1.3.0')")
    if not re.fullmatch(r"[0-9a-f]{64}", sha256):
        sys.exit(f"Invalid SHA-256 '{sha256}' (expected 64 lowercase hex characters)")
    if not (download_url.startswith("https://") or download_url.startswith("\\\\")):
        sys.exit("download_url must be an https:// URL or UNC network share path (\\\\server\\share\\...)")

    url = os.environ["ODOO_URL"].rstrip("/")
    db = os.environ["ODOO_DB"]
    user = os.environ["ODOO_USER"]
    password = os.environ["ODOO_PASSWORD"]

    common = xmlrpc.client.ServerProxy(f"{url}/xmlrpc/2/common", allow_none=True)
    uid = common.authenticate(db, user, password, {})
    if not uid:
        sys.exit("Odoo authentication failed")

    models = xmlrpc.client.ServerProxy(f"{url}/xmlrpc/2/object", allow_none=True)
    updates = {
        "client_version": version,
        "client_download_url": download_url,
        "client_download_sha256": sha256,
    }
    for key, val in updates.items():
        ids = models.execute_kw(db, uid, password, "hp.settings", "search", [[("name", "=", key)]], {"limit": 1})
        if not ids:
            sys.exit(f"hp.settings record '{key}' not found in database '{db}'")
        models.execute_kw(db, uid, password, "hp.settings", "write", [ids, {"char_value": val}])
        print(f"Updated hp.settings.{key} = {val}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
