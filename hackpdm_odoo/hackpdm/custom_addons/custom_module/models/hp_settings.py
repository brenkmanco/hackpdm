from odoo import fields, models
import datetime as dt
import numpy as np

def _return_or_none(value, type):
    try:
        return type(value)
    except (ValueError, TypeError):
        return None

def cast_to_bool(s:str):
    return _return_or_none(s, bool)
def cast_to_int(s:str):
    return _return_or_none(s, int)
def cast_to_float(s:str):
    return _return_or_none(s, float)
def cast_to_date(s:str):
    return _return_or_none(s, dt.datetime)
def cast_to_binary(s:str):
    return _return_or_none(s, bytes)


class hp_settings(models.Model):
    #base fields
    _name = 'hp.settings'
    _description = 'hp settings'
    _inherit = 'hp.common.model'

    #fields
    name = fields.Char(string='Name', required=True)
    description = fields.Text(string="Description")
    restrict_properties = fields.Boolean(string='restrict properties', compute="_compute_other")
    restrict_types = fields.Boolean(string='restrict types', compute="_compute_other")
    seconds_tolerance = fields.Float(string='seconds tolerance', compute="_compute_other")

    def _compute_other(self):
        for rec in self:
            mystr = self.env["ir.config_parameter"].sudo().search([("key", "=", "restrict_properties")]).value
            rec.restrict_properties = cast_to_bool(mystr)
            mystr = self.env["ir.config_parameter"].sudo().search([("key", "=", "restrict_types")]).value
            rec.restrict_types = cast_to_bool(mystr)
            mystr = self.env["ir.config_parameter"].sudo().search([("key", "=", "second_tolerance")]).value
            rec.seconds_tolerance = cast_to_float(mystr)

