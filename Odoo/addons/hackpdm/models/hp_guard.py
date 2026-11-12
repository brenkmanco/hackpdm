"""Server-side safeguards shared by the hackpdm models.

Clients turn vault records into local file paths, so the names stored here must be safe to use
as a single Windows/Linux path segment, and a client bug or a hand-made RPC call must not be able
to take over another user's checkout or run maintenance jobs."""
import logging
import re

from odoo.exceptions import AccessError, ValidationError
from odoo.tools import sql

_logger = logging.getLogger(__name__)

# characters Windows refuses in a file name, plus the path separators of every OS
_FORBIDDEN_CHARS = re.compile(r'[<>:"/\\|?*\x00-\x1f\x7f]')
# device names Windows reserves, with or without an extension ("CON", "nul.txt")
_RESERVED_STEMS = frozenset(
    ["CON", "PRN", "AUX", "NUL"]
    + [f"COM{i}" for i in range(1, 10)]
    + [f"LPT{i}" for i in range(1, 10)]
)
MAX_NAME_LENGTH = 255


def is_pdm_admin(env):
    """Superuser mode or a member of Settings (base.group_system): may bypass the user-level guards."""
    return env.su or env.user.has_group("base.group_system")


def require_admin(env, what):
    """Maintenance/migration jobs touch every record and commit mid-transaction: administrators only."""
    if not is_pdm_admin(env):
        raise AccessError(f"Only an administrator can run '{what}'.")


def name_problem(name):
    """Why name cannot be a file or folder name, or None when it can."""
    if not name or not name.strip():
        return "the name is empty"
    if len(name) > MAX_NAME_LENGTH:
        return f"the name is longer than {MAX_NAME_LENGTH} characters"
    if name in (".", ".."):
        return f"'{name}' is not a valid name"
    bad = _FORBIDDEN_CHARS.search(name)
    if bad:
        char = bad.group(0)
        shown = char if char.isprintable() else f"\\x{ord(char):02x}"
        return f"the name contains '{shown}', which is not allowed in file names"
    if name != name.rstrip(" ."):
        return "the name ends with a space or a dot"
    if name.split(".", 1)[0].strip().upper() in _RESERVED_STEMS:
        return f"'{name}' is a reserved device name on Windows"
    return None


def check_name(name, kind):
    """Raises ValidationError when name is unsafe as a single path segment."""
    problem = name_problem(name)
    if problem:
        raise ValidationError(f"Invalid {kind} name {name!r}: {problem}.")


def create_case_insensitive_unique_index(cr, table, index_name, scope_column):
    """Unique (scope, lower(name)) index, so two clients cannot both create "Part.SLDPRT" and
    "part.sldprt" in one folder (they are the same file on Windows). Skipped, with a warning, while
    existing rows already break it: those have to be renamed by hand first."""
    if sql.index_exists(cr, index_name):
        return
    scope = f'COALESCE("{scope_column}", 0)'
    cr.execute(
        f'SELECT {scope}, lower(name), count(*) FROM "{table}" '
        f"GROUP BY 1, 2 HAVING count(*) > 1 LIMIT 5"
    )
    duplicates = cr.fetchall()
    if duplicates:
        _logger.warning(
            "%s: not creating %s, these (%s, name) pairs differ only by case: %s",
            table, index_name, scope_column, duplicates,
        )
        return
    sql.create_unique_index(cr, index_name, table, [scope, "lower(name)"])
