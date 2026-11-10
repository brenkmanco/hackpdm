#!/usr/bin/env python3
"""Seed a fresh hackpdm database with a small directory tree, entries and versions
so the client smoke test (HACKPDM_SMOKE_TEST) has something to navigate.

Usage: seed_smoke_data.py URL DB USER PASSWORD

Creates (idempotently, keyed on names):
    root                      <- must be hp.directory id 1, the client's tree root
    root/Projects
    root/Projects/Parts       <- bracket.txt, plate.txt, readme.txt (one version each)
    root/Library
"""
import base64
import sys
import xmlrpc.client

url, db, user, password = sys.argv[1:5]
common = xmlrpc.client.ServerProxy(f"{url}/xmlrpc/2/common", allow_none=True)
uid = common.authenticate(db, user, password, {})
if not uid:
    sys.exit("authentication failed")
models = xmlrpc.client.ServerProxy(f"{url}/xmlrpc/2/object", allow_none=True)


def call(model, method, *args, **kwargs):
    return models.execute_kw(db, uid, password, model, method, list(args), kwargs)


def ensure(model, domain, values):
    ids = call(model, "search", domain, limit=1)
    return ids[0] if ids else call(model, "create", values)


root = ensure("hp.directory", [("name", "=", "root"), ("parent_id", "=", False)], {"name": "root"})
if root != 1:
    sys.exit(f"root directory must be id 1 (the client loads hp.directory 1 as its root), got {root}")

projects = ensure("hp.directory", [("name", "=", "Projects"), ("parent_id", "=", root)],
                  {"name": "Projects", "parent_id": root})
parts = ensure("hp.directory", [("name", "=", "Parts"), ("parent_id", "=", projects)],
               {"name": "Parts", "parent_id": projects})
ensure("hp.directory", [("name", "=", "Library"), ("parent_id", "=", root)],
       {"name": "Library", "parent_id": root})

txt = ensure("hp.type", [("file_ext", "=", "txt")], {"file_ext": "txt", "description": "text file"})

for name in ("bracket.txt", "plate.txt", "readme.txt"):
    entry = ensure("hp.entry", [("name", "=", name), ("dir_id", "=", parts)],
                   {"name": name, "dir_id": parts, "type_id": txt})
    if call("hp.version", "search_count", [("entry_id", "=", entry)]):
        continue
    content = f"hackpdm smoke test file {name}\n".encode()
    version = call("hp.version", "create", {"entry_id": entry, "dir_id": parts})
    attachment = call("ir.attachment", "create", {
        "name": name,
        "datas": base64.b64encode(content).decode(),
        "res_model": "hp.version",
        "res_id": version,
    })
    call("hp.version", "write", [version], {"attachment_id": attachment})

summary = {
    "directories": call("hp.directory", "search_count", []),
    "entries": call("hp.entry", "search_count", []),
    "versions": call("hp.version", "search_count", []),
}
print(f"seeded: {summary}")
