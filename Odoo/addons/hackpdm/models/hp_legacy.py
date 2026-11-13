"""Connections to the previous HackPDM storage, used only by the one-off administrator migrations.

Normal operation never touches this module. The addresses and logins are read from system parameters
(Settings > Technical > System Parameters), never from the code:

    hackpdm.legacy_webdav_url       the old WebDAV root, such as https://old-server/webdav
    hackpdm.legacy_webdav_user      its login
    hackpdm.legacy_webdav_password  its password
    hackpdm.legacy_db_url           the old database, such as postgresql://user:password@host:5432/hackpdm

``webdav3`` and ``sqlalchemy`` are imported only when a migration runs, so a server that never migrates
does not need them installed.
"""
import io
import logging
import urllib.parse

from odoo.exceptions import UserError

_logger = logging.getLogger(__name__)


def _param(env, key):
    """Reads a required system parameter.

    Args:
        env (odoo.api.Environment): The environment to read ``ir.config_parameter`` with.
        key (str): The parameter name, such as ``hackpdm.legacy_db_url``.

    Returns:
        str: The parameter's value.

    Raises:
        UserError: The parameter is not set.
    """
    value = env["ir.config_parameter"].sudo().get_param(key)
    if not value:
        raise UserError(f"Set the system parameter '{key}' before running this migration.")
    return value


class LegacyWebDav:
    """The old WebDAV file store: one folder per entry id, one file per version (``<version id>.<ext>``)."""

    def __init__(self, env):
        from webdav3.client import Client  # only needed for this migration

        self.client = Client({
            "webdav_hostname": _param(env, "hackpdm.legacy_webdav_url"),
            "webdav_login": _param(env, "hackpdm.legacy_webdav_user"),
            "webdav_password": _param(env, "hackpdm.legacy_webdav_password"),
            "webdav_override_methods": {"check": "GET"},
            "webdav_timeout": 30,
        })

    def files_in_entry(self, entry_id):
        """Lists the file names stored for an entry, or an empty list when the entry has no folder."""
        try:
            return self.client.list(urllib.parse.quote(f"/{entry_id}"))
        except Exception:
            _logger.error("entry '%s' doesn't exist", entry_id)
            return []

    def get_file_from_webdav(self, entry_id, version_id, file_extension):
        """Downloads one version's file, or returns ``b''`` when it is missing or the download fails."""
        file_path = urllib.parse.quote(f"/{entry_id}/{version_id}.{file_extension}")
        try:
            buffer = io.BytesIO()
            self.client.download_from(remote_path=file_path, buff=buffer)
            return buffer.getvalue()
        except Exception as e:
            if f"{version_id}.{file_extension}" in self.files_in_entry(entry_id):
                _logger.error(e)
            else:
                _logger.error("file version '%s' doesn't exist", version_id)
        return b""


class LegacyDatabase:
    """The old HackPDM PostgreSQL database, read through SQLAlchemy."""

    def __init__(self, env):
        from sqlalchemy import create_engine  # only needed for this migration

        self.engine = create_engine(_param(env, "hackpdm.legacy_db_url"))
        self.connection = self.engine.connect()

    def preview_image(self, version_id):
        """Reads a version's preview image from the old ``hp_version`` table.

        Args:
            version_id (int): The version id, the same in the old and the new database.

        Returns:
            bytes | None: The image, or None when the version has none or the query failed.
        """
        from sqlalchemy import text

        try:
            row = self.connection.execute(
                text("SELECT preview_image FROM hp_version WHERE version_id = :id"), {"id": version_id}).first()
            return bytes(row[0]) if row and row[0] else None
        except Exception as e:
            _logger.error("preview query failed for version %s: %s", version_id, e)
            return None
