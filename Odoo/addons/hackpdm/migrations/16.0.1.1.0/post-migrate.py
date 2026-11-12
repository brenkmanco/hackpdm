"""16.0.1.1.0: the checkout moved from hp.entry to the entry's latest hp.version.

hp.entry.checkout_user / checkout_date / checkout_node are now related to latest_version_id and
is_checked_out is computed from them. The old hp_entry columns are left in place by Odoo; this copies
what they hold onto each checked-out entry's latest version, so nothing checked out is released by the
upgrade."""


def migrate(cr, version):
    cr.execute(
        """SELECT column_name FROM information_schema.columns
           WHERE table_name = 'hp_entry' AND column_name IN ('checkout_user', 'checkout_date', 'checkout_node', 'is_checked_out')"""
    )
    columns = {row[0] for row in cr.fetchall()}
    if "checkout_user" not in columns:
        return
    held = "e.checkout_user IS NOT NULL"
    if "is_checked_out" in columns:
        held += " AND e.is_checked_out"
    cr.execute(
        f"""UPDATE hp_version v
               SET checkout_user = e.checkout_user,
                   checkout_date = {'e.checkout_date' if 'checkout_date' in columns else 'NULL'},
                   checkout_node = {'e.checkout_node' if 'checkout_node' in columns else 'NULL'}
              FROM hp_entry e
             WHERE {held}
               AND v.id = (SELECT MAX(id) FROM hp_version WHERE entry_id = e.id)"""
    )
