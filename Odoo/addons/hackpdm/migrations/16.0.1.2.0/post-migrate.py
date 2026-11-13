"""16.0.1.2.0: access moved from base.group_user to the HackPDM groups (security/hackpdm_groups.xml).

Before this version every internal user could check out and commit. So nobody loses that on upgrade, every
existing internal user is made a HackPDM Engineer; demote users to Viewer afterwards where wanted."""
from odoo import SUPERUSER_ID, api


def migrate(cr, version):
    env = api.Environment(cr, SUPERUSER_ID, {})
    engineer = env.ref("hackpdm.group_pdm_user")
    internal = env["res.users"].with_context(active_test=False).search([("share", "=", False)])
    engineer.write({"users": [(4, user.id) for user in internal]})
