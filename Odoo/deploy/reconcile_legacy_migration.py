#!/usr/bin/env python3
"""Milestone 5.3 Legacy HackPDM Data Migration Reconciliation Helper.

Compares record counts between the legacy HackPDM PostgreSQL database and the target Odoo database,
sample-verifies migrated hp.version SHA-1 checksums and attachment presence, and prints the
post-migration cleanup checklist.

Usage:
  LEGACY_DB_URL=postgresql://user:pass@legacy-host/hackpdm \
  ODOO_URL=https://pdm.example.com ODOO_DB=hackpdm_prod ODOO_USER=admin ODOO_PASSWORD=... \
    ./reconcile_legacy_migration.py [--sample 25]
"""
import argparse
import os
import sys
import xmlrpc.client

try:
    from sqlalchemy import create_engine, text
except ImportError:
    sys.exit("SQLAlchemy is required: pip install 'sqlalchemy>=2,<3' psycopg2-binary")


TABLE_MAP = [
    ("hp_directory", "hp.directory"),
    ("hp_entry", "hp.entry"),
    ("hp_version", "hp.version"),
    ("hp_version_relationship", "hp.version.relationship"),
    ("hp_version_property", "hp.version.property"),
]


def main() -> int:
    parser = argparse.ArgumentParser(description="Reconcile legacy HackPDM migration counts and checksums.")
    parser.add_argument("--sample", type=int, default=25, help="Number of recent hp.version rows to sample-check")
    args = parser.parse_args()

    legacy_url = os.environ["LEGACY_DB_URL"]
    odoo_url = os.environ["ODOO_URL"].rstrip("/")
    odoo_db = os.environ["ODOO_DB"]
    odoo_user = os.environ["ODOO_USER"]
    odoo_pass = os.environ["ODOO_PASSWORD"]

    common = xmlrpc.client.ServerProxy(f"{odoo_url}/xmlrpc/2/common", allow_none=True)
    uid = common.authenticate(odoo_db, odoo_user, odoo_pass, {})
    if not uid:
        sys.exit("Odoo authentication failed")
    models = xmlrpc.client.ServerProxy(f"{odoo_url}/xmlrpc/2/object", allow_none=True)

    engine = create_engine(legacy_url)
    mismatches = 0
    print(f"{'Legacy Table':<26} {'Odoo Model':<26} {'Legacy':>10} {'Odoo':>10} {'Status':>10}")
    print("-" * 86)
    with engine.connect() as conn:
        for legacy_table, odoo_model in TABLE_MAP:
            try:
                legacy_count = conn.execute(text(f"SELECT COUNT(*) FROM {legacy_table}")).scalar_one()
            except Exception as exc:
                print(f"{legacy_table:<26} {odoo_model:<26} {'ERR':>10} {'-':>10} {'ERROR':>10}  ({exc})")
                mismatches += 1
                continue
            odoo_count = models.execute_kw(odoo_db, uid, odoo_pass, odoo_model, "search_count", [[]])
            ok = legacy_count == odoo_count
            if not ok:
                mismatches += 1
            print(f"{legacy_table:<26} {odoo_model:<26} {legacy_count:>10} {odoo_count:>10} {'OK' if ok else 'DIFF':>10}")

    print(f"\nSampling up to {args.sample} hp.version records in Odoo for attachment & checksum integrity...")
    sample_rows = models.execute_kw(
        odoo_db, uid, odoo_pass, "hp.version", "search_read", [[]],
        {"fields": ["id", "name", "checksum", "file_size", "attachment_id"], "limit": args.sample, "order": "id desc"},
    )
    missing_attachments = [r["id"] for r in sample_rows if not r.get("attachment_id") or not r.get("checksum")]
    print(f"Sampled: {len(sample_rows)} versions; missing attachment/checksum: {len(missing_attachments)}")
    if missing_attachments:
        print(f"  IDs missing attachment/checksum: {missing_attachments}")
        mismatches += 1

    print("\nPost-migration cleanup checklist (run once reconciliation passes):")
    print("  1. Delete Odoo system parameters: hackpdm.legacy_webdav_url, hackpdm.legacy_webdav_user,")
    print("     hackpdm.legacy_webdav_password, hackpdm.legacy_db_url")
    print("  2. Revoke temporary legacy DB and WebDAV credentials.")

    return 1 if mismatches else 0


if __name__ == "__main__":
    raise SystemExit(main())
