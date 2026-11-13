import io
import logging

from odoo import fields, models, api
from odoo.exceptions import UserError

from . import hp_guard
from . import hp_legacy

import hashlib as hash
import base64
import logging
import urllib.parse
# pyrefly: ignore [missing-import]
import magic


class hp_common_model(models.AbstractModel):
    #base fields
    _name = 'hp.common.model'
    _description = 'commmon model functions for hp models'

    commit_id = fields.Many2one(
        comodel_name='hp.pdm.commit',
        string='commit id',
    )

    def _form_dict(self, value, arr, depth):
       data = {"id": value.id, "name": value.name if "name" in value._fields else "", "model": value._name}
       if arr:
           data["path"] = arr
       return data


    def _help_gather(self, value, parts, arr, depth):
       if len(parts) == 1:
           if isinstance(value, models.BaseModel):
               if len(value) == 1:
                   return self._form_dict(value, arr, depth)
               else:
                   return [self._form_dict(rec, arr, depth) for rec in value]
           else:
               return {"value": value, "path": arr} if len(arr) > 0 else value

    def _check_field_path(self, path):
        """Each step of a dotted field path ("dir_id.parent_id.name") must exist and be readable by
        the caller: record[field] in Python skips the field-group checks read() makes."""
        model = self
        for name in path.split("."):
            field = model._fields.get(name)
            if field is None:
                raise UserError(f"Unknown field '{name}' on {model._name} (in '{path}').")
            model.check_access_rights("read")
            model.check_field_access_rights("read", [name])
            if field.relational:
                model = self.env[field.comodel_name]

    def _resolve_field(self, record, parts, arr, depth):
        """Recursively resolve dot-notated fields into rich structures."""
        field = parts[0]
        value = record[field]

        if len(parts) == 1: # Terminal field → return raw value
            return self._help_gather(value, parts, arr, depth)

         # Still more parts to traverse
        rest = parts[1:]
        if value._name and len(value) == 1: # Many2one
            return [    
                    self._resolve_field(
                        value, 
                        rest, 
                        arr + [self._form_dict(value, None, depth)], 
                        depth+1
                    )
                ]
        else: # One2many / Many2many
            return [
                    self._resolve_field(
                        child, 
                        rest, 
                        arr + [self._form_dict(child, None, depth)], 
                        depth+1
                    ) 
                    for child in value
                ]

    @api.model
    def smart_read(self, domain, ids, fields):
        """Reads records with dotted field paths resolved through relations, for every hp model.

        Each dotted path ("dir_id.parent_id.name") is checked first: every step must be a real field
        the caller may read, otherwise nothing is read. Plain fields come back as their value; a
        relational field as ``{"id", "name", "model"}`` (a list of those for x2many); a dotted path as
        nested values carrying a "path" of the records it went through.

        Args:
            domain (list | None): Odoo search domain. Combined with ``ids`` when both are given.
            ids (list[int] | None): Record ids to read.
            fields (list[str] | None): Field names or dotted field paths. None or empty reads only ids.

        Returns:
            list[dict]: One dict per record, always with "id", plus one key per requested field;
            ``[]`` when neither ``domain`` nor ``ids`` is given or nothing matches.

        Raises:
            UserError: A field path names a field that does not exist.
            AccessError: The caller may not read a model or field along a path.
        """
        # domain=None, ids=None, fields=None
        logging.info(f"\nfields: {fields}\nmodel_name: {self._name}\ndomain: {domain}\nIDs: {ids}")

        Model = self

        records = None

        if not domain and not ids:
            return []

        fields = list(fields or [])
        for field in fields:
            self._check_field_path(field)

        if not domain:
            records = Model.browse(ids)
        elif not ids:
            records = Model.search(domain or [])
        else:
            records = Model.search(domain + [('id', 'in', ids)])

        if not records or len(records) == 0:
            return []


        results:list[dict[str, Any]] = []

        for rec in records:
            # always identify the record, like read() does; callers that asked only for other
            # fields could not tell which record they got (an existing entry looked new)
            data:dict[str, Any] = {"id": rec.id}
            for field in fields or []:
                parts:list[str] = field.split(".")
                data[field] = self._resolve_field(rec, parts, [], 0)
            results.append(data)
        return results

    @api.model
    def fast_read(self, ids, fields):
        """Reads plain field values for records, keyed by id, through ``read()`` (access rules apply).

        Args:
            ids (list[int]): Record ids.
            fields (list[str]): Field names; names the model does not have are left out silently.

        Returns:
            dict[str, dict]: ``{"<id>": {field: value}}``; ``{}`` when ``ids`` or ``fields`` is empty.

        Raises:
            AccessError: The caller may not read the records or one of the fields.
        """
        if not ids or not fields:
            return {}
        known = [field for field in fields if field in self._fields]
        return {
            str(row["id"]): {field: row[field] for field in known if field in row}
            for row in self.browse(ids).read(known)
        }

    @api.model
    def related_browse(self, ids, field_name, fields):
        """Follows a relational field from some records and reads the records it leads to.

        Args:
            ids (list[int]): Ids of records of this model.
            field_name (str): Relational field (or dotted path) to follow, e.g. "version_ids".
            fields (list[str]): Fields to read on the related records.

        Returns:
            list[dict]: ``read()`` rows of the related records (each once); ``[]`` when ``ids`` is empty.

        Raises:
            UserError: ``field_name`` names a field that does not exist.
            AccessError: The caller may not read along the path or the related records.
        """
        if not ids:
            return []

        self._check_field_path(field_name)
        recordset = self.browse(ids)
        related_records = recordset.mapped(field_name)
        return related_records.read(fields)

    @api.model
    def related_search_browse(self, search, field_name, fields):
        """Like :meth:`related_browse`, but finds the starting records with a search.

        Args:
            search (list): Odoo search domain for records of this model.
            field_name (str): Relational field (or dotted path) to follow.
            fields (list[str]): Fields to read on the related records.

        Returns:
            list[dict]: ``read()`` rows of the related records; ``[]`` when ``search`` is empty.

        Raises:
            UserError: ``field_name`` names a field that does not exist.
            AccessError: The caller may not read along the path or the related records.
        """
        if not search:
            return []

        self._check_field_path(field_name)
        recordset = self.search(search)
        related_records = recordset.mapped(field_name)
        return related_records.read(fields)

    @api.model
    def create_read(self, vals, fields=None):
        """Creates one record and reads it back in the same call.

        Args:
            vals (dict): Field values for the new record (the model's own create() rules apply).
            fields (list[str], optional): Fields to read back. Defaults to every field.

        Returns:
            list[dict]: A one-item ``read()`` result for the new record.

        Raises:
            AccessError: The caller may not create records of this model.
            ValidationError: The values break a constraint (e.g. an unsafe file name).
        """
        record = self.create(vals)
        # if no fields specified return all readable fields
        if not fields:
            fields = record.fields_get().keys()
        return record.read(fields)

    @api.model
    def _create_attachment(self, file_contents:bytes, field_name:str):
        file_name = f"{self.id}.{self.name}"
        file_contents_b64 = base64.b64encode(file_contents).decode('utf-8')
        mime_type = magic.Magic(mime=True).from_buffer(file_contents)

        values = {
            'name': file_name,
            'type': 'binary',
            'datas': file_contents_b64,
            'res_model': self._name,
            'res_field': field_name,
            'res_id': self.id,
            'mimetype': mime_type,
        }
        try:
            attachment = self.env['ir.attachment'].create(values)
            return attachment
        except Exception as e:
            logging.error(e)
            return None

    @api.model
    def _get_attachments(self, record, field_name):
        attachment_model = self.env['ir.attachment']
        attachments = attachment_model.search([
            #('res_model', '=', self._name),
            ('res_id', '=', record.id),
            ('res_field', '=', field_name)
        ])
        return attachments

    def _import_versions(self, web_dav):
        all_records = self.env["hp.version"].search([])

        for record in all_records:
            #search for ir.attachment records for hp.version
            attachment = record._get_attachments(record, "file_contents")
            if attachment and len(attachment)>0:
                logging.info(f"version record {attachment[0].res_id} has attachment id {attachment[0].id} named: {attachment[0].name}")
                continue
            file_contents = web_dav.get_file_from_webdav(record.entry_id.id, record.id, record.file_ext)
            if file_contents != b'':
                try:
                    attachment = record._create_attachment(file_contents, "file_contents")
                except:
                    logging.warning(f"didn't create attachment {record.id}.{record.name}")
                if (attachment != None):
                    logging.info(f"attachment id {attachment.id} created for version record {attachment.res_id} named: {attachment.name}")
            else:
                logging.warning(f"didn't create attachment {record.id}.{record.name}")
            self.env.cr.commit()

    @api.model
    @api.depends('name')
    def _import_versions_image(self, hackpdm):
        all_records = self.env[self._name].search([])

        for record in all_records:
            attachment = record._get_attachments(record, "preview_image")
            if attachment and len(attachment)>0:
                logging.info(f"version record {attachment[0].res_id} has preview_image")
                continue

            try:
                image = hackpdm.preview_image(record.id)
                if image:
                    attachment = record._create_attachment(image, "preview_image")
                    if (attachment != None):
                        logging.info(f"attachment id {attachment.id} created for version record {attachment.res_id} named: {attachment.name}")
                    else:
                        logging.warning(f"didn't create preview_image attachment {record.id}.{record.name}")
                else:
                    logging.warning(f"empty preview_image in db")
            except Exception as e:
                logging.error(e)
            self.env.cr.commit()


    @api.model
    @api.depends('entry_id', 'file_ext', 'name')
    def migrate_webdav_files_to_records(self):
        """One-off migration: copies preview images from the old HackPDM database into attachments.

        Administrators only. Commits after every record. Despite the name, the WebDAV file import is
        commented out; only ``_import_versions_image`` runs. The old database comes from the
        ``hackpdm.legacy_db_url`` system parameter (see ``hp_legacy``).

        Args:
            None.

        Returns:
            None: nothing.

        Raises:
            AccessError: The caller is not an administrator.
        """
        hp_guard.require_admin(self.env, "migrate_webdav_files_to_records")
        hackpdm = hp_legacy.LegacyDatabase(self.env)
        self._import_versions_image(hackpdm)
