#!/usr/bin/env python3
"""Publishes a new HackPDM client version to Odoo's hp.settings records over XML-RPC.

Updates:
  - client_version          (required version or range; clients outside are blocked)
  - client_latest_version   (optional recommended version; compatible older clients are prompted)
  - client_download_url     (https:// URL or UNC network share path to the installer)
  - client_download_sha256  (64-char lowercase SHA-256 hex digest)

Usage:
  ODOO_URL=https://pdm.example.com ODOO_DB=hackpdm_prod ODOO_USER=admin ODOO_PASSWORD=... \\
    ./publish_client_release.py ">=1.2.0, <2.0.0" \\
      https://github.com/moonxbears/wyrmpdm/releases/download/v1.2.0/HackPDM-1.2.0-win-x64-setup.exe \\
      <sha256> --latest-version 1.2.0
"""
import argparse
import os
import re
import sys
import xmlrpc.client


def upsert_char_setting(models, db: str, uid: int, password: str, key: str, value: str, description: str = "") -> None:
    ids = models.execute_kw(db, uid, password, "hp.settings", "search", [[("name", "=", key)]], {"limit": 1})
    if ids:
        models.execute_kw(db, uid, password, "hp.settings", "write", [ids, {"char_value": value}])
        print(f"Updated hp.settings.{key} = {value}")
    else:
        models.execute_kw(
            db,
            uid,
            password,
            "hp.settings",
            "create",
            [{"name": key, "type": "char", "char_value": value, "description": description}],
        )
        print(f"Created hp.settings.{key} = {value}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Publish a HackPDM client release to Odoo hp.settings.")
    parser.add_argument("version", help="Required client_version or range (e.g. '1.2.0', '>=1.2.0, <2.0.0', '1.1.0 - 1.3.0')")
    parser.add_argument("download_url", help="Installer https:// URL or UNC network share path")
    parser.add_argument("sha256", help="Installer SHA-256 hex digest (64 hex chars)")
    parser.add_argument(
        "--latest-version",
        dest="latest_version",
        default=None,
        help="Optional recommended version (e.g. '1.2.0') for non-blocking update prompts",
    )
    args = parser.parse_args()

    version = args.version.strip()
    download_url = args.download_url.strip()
    sha256 = args.sha256.strip().lower()
    latest_version = args.latest_version.strip() if args.latest_version else None

    if not version or not re.fullmatch(r"[0-9.<>=,\s\-;]+", version):
        sys.exit(f"Invalid version or range '{version}' (e.g. '1.2.0', '>=1.2.0, <2.0.0', or '1.1.0 - 1.3.0')")
    if latest_version is not None and not re.fullmatch(r"\d+\.\d+\.\d+(?:\.\d+)?", latest_version):
        sys.exit(f"Invalid --latest-version '{latest_version}' (expected e.g. '1.2.0')")
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
    upsert_char_setting(
        models, db, uid, password, "client_version", version,
        "required HackPDM client version or range; clients outside this range are denied access until updated",
    )
    if latest_version is not None:
        upsert_char_setting(
            models, db, uid, password, "client_latest_version", latest_version,
            "latest published HackPDM client version; compatible older clients are prompted to update without blocking login",
        )
    upsert_char_setting(
        models, db, uid, password, "client_download_url", download_url,
        "where clients download the installer (https URL, or a file or network-share path)",
    )
    upsert_char_setting(
        models, db, uid, password, "client_download_sha256", sha256,
        "SHA-256 of the installer at client_download_url; clients refuse a file that does not match",
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
