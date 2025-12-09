import io
import os
from odoo import models, fields, api, Command
from webdav3.client import Client
import hashlib as hash
import base64
import logging
import urllib.parse
import magic
import pdb

import sqlalchemy
from sqlalchemy import MetaData, Table, create_engine, select, update
from sqlalchemy.orm import Mapped, sessionmaker, relationship

_logger = logging.getLogger(__name__)

class Database:
    def __init__(self, db_username:str, db_password:str, db_name:str, db_hostname:str, db_port):
        self.sessionURL = f'postgresql://{db_username}:{db_password}@{db_hostname}:{db_port}/{db_name}'

        self.engine = create_engine(self.sessionURL)
        self.connection = self.engine.connect()
        self.metadata = MetaData()
        self._Session = sessionmaker(bind=self.engine)

    def get_table(self, table_name):
        return Table(table_name, self.metadata, autoload_with=self.engine)

    def start_session(self):
        self.session = self._Session()

    def execute(self, statement):
        return self.session.execute(statement)
class WebDav():
    logging.basicConfig(level=logging.DEBUG)
    logger = logging.getLogger(__name__)
    local_path = "~/dev/tempDirectory"

    def __init__(self, client:Client):
        self.client = client
    def __init__(self, options:dict[str, any]):
        self.client = Client(options)
    def __init__(self):
        options = {
            'webdav_hostname': "http://hackpdm.zip.azi/webdav",
            'webdav_port': 80,
            'webdav_login': "mtaylor",
            'webdav_password': "mtaylor",
            'webdav_override_methods': {
                'check': 'GET',
            },
            'webdav_timeout': 30,
        }
        self.client = Client(options)

    def files_in_entry(self, entry_id:int):
        try:
            directory_path = urllib.parse.quote(f"/{entry_id}")
            return self.client.list(directory_path)
        except:
            logging.error(f"entry '{entry_id}' doesn't exist")
            return []

    def get_file_from_webdav(self, entry_id:int, version_id:int, file_extension:str):
        file_path = urllib.parse.quote(f"/{entry_id}/{version_id}.{file_extension}")
        file_info = {}
        try:
            file_info = self.client.info(file_path)
            buffer = io.BytesIO()

            self.client.download_from(remote_path=file_path, buff=buffer)
            buffer.seek(0)
            file_contents = buffer.read()

            return file_contents
        except Exception as e:
            if f"{version_id}.{file_extension}" in self.files_in_entry(entry_id):
                logging.error(e)
            else:
                logging.error(f"file version '{version_id}' doesn't exist")
        return b''

        #print(f"File information: {file_info}")


class hp_version(models.Model):

    _name = 'hp.version'
    _description = 'hp version'
    _inherit = 'hp.common.model'

    name = fields.Char(
        related='entry_id.name',
        string='file name',
    )
    checksum = fields.Char(
        related='attachment_id.checksum',
        string='sha checksum',
    )
    file_ext = fields.Char(
        related='entry_id.type_id.file_ext',
        string='file extension',
    )
    directory_complete_name = fields.Char(
        related='entry_id.directory_complete_name',
        string='directory folder pathway',
    )
    windows_complete_name = fields.Char(
        related='entry_id.windows_complete_name',
        string='windows directory path name',
    )
    windows_complete_path = fields.Char(
        related='entry_id.windows_complete_path',
        string='windows directory path',
    )
    checkout_date = fields.Datetime(
        related='entry_id.checkout_date',
        string='checkout date',
    )
    file_modify_stamp = fields.Datetime(
        string='modified date',
        default=lambda self:fields.Datetime.now(),
        #required=True,
    )

    file_size = fields.Integer(
        related='attachment_id.file_size',
        string='file size',
        store=True,
    )

    preview_image = fields.Image('preview image')

    file_contents = fields.Binary(
        string='file contents',
        attachment=True,
    )

    deleted = fields.Boolean(
        related='entry_id.deleted',
        string='entry deleted',
    )

    dir_id = fields.Many2one(
        comodel_name='hp.directory',
        related='entry_id.dir_id',
        string='directory',
    )
    entry_id = fields.Many2one(
        comodel_name='hp.entry',
        string='entry'
    )
    node_id = fields.Many2one(
        comodel_name='hp.node',
        string='node'
    )
    checkout_user = fields.Many2one(
        related='entry_id.checkout_user',
        string='user that checked out entry'
    )
    checkout_node = fields.Many2one(
        related='entry_id.checkout_node',
        string='node that has checked out entry'
    )
    attachment_id = fields.Many2one(
        comodel_name='ir.attachment',
        compute='_compute_attachment',
        store=True,
        string='attachment',
    )
    release_id = fields.Many2one(
        comodel_name='hp.release',
    )

    attachment_ids = fields.One2many(
        comodel_name='ir.attachment',
        inverse_name='res_id',
        domain=[('res_model', '=', 'hp.version'), ('type', '=', 'binary')],
        auto_join=True,
        string="Documents",
    )
    parent_ids = fields.One2many(
        comodel_name='hp.version.relationship',
        inverse_name='child_id',
        string='parent versions',
    )
    child_ids = fields.One2many(
        comodel_name='hp.version.relationship',
        inverse_name='parent_id',
        string='child versions',
    )
    version_property_ids = fields.One2many(
        comodel_name='hp.version.property',
        inverse_name='version_id',
        string='version properties',
    )

    node_latest_ids = fields.Many2many(
        comodel_name='hp.node',
    )

    @api.depends('file_contents')
    def _compute_attachment(self):
        for rec in self:
            rec.attachment_id = self.env['ir.attachment'].search([
                ('res_model', '=', rec._name),
                ('res_id', '=', rec.id),
                ('res_field', '=', 'file_contents')
            ], limit=1)

    @api.depends('attachment_id')
    def _compute_md5sum(self):
        for record in self:

            attachment = self.attachment_id
            if attachment:
                record.md5sum = attachment.checksum
            else:
                record.md5sum = False


    @api.model
    def get_recursive_dependency(self, version_ids):
        if not version_ids:
            return []
        if isinstance(version_ids, int):
            version_ids = [version_ids]
        elif not isinstance(version_ids, list):
            version_ids = list(version_ids)

        # using raw sql for efficient recursive dependency traversal
        # this query finds all version IDs (initial + children + grandchildren, etc.)
        query_versions = """
            WITH RECURSIVE dependency_versions (version_id) AS (
                SELECT id FROM hp_version WHERE id = ANY(%s) -- start with initial versions
            UNION -- Use UNION ALL if cycles are impossible or handled; UNION removes duplicates earlier
                SELECT c.child_id
                FROM dependency_versions p, hp_version_relationship c
                JOIN hp_version vc ON vc.id = c.child_id
                JOIN hp_entry ec ON ec.id = vc.entry_id
                WHERE c.parent_id = p.version_id AND ec.deleted = false -- Recursively find children of non-deleted entries
            )
            SELECT DISTINCT version_id FROM dependency_versions;
        """
        try:
            self.env.cr.execute(query_versions, (version_ids,))
            version_results = self.env.cr.fetchall()
            all_dep_version_ids = {row[0] for row in version_results}

            if not all_dep_version_ids:
                return []
            # Now get unique entry_ids for these versions using the ORM for simplicity
            return all_dep_version_ids

        except Exception as e:
            _logger.error(f"Error fetching recursive dependencies for versions {version_ids}: {e}")
            return []

    @api.model
    def get_recursive_dependency_versions(self, version_ids):
        try:
            if not version_ids:
                return []
            if isinstance(version_ids, int):
                version_ids = [version_ids]
            elif not isinstance(version_ids, list):
                version_ids = list(version_ids)

            complete_version_ids = self.get_recursive_dependency(version_ids)
            return list(complete_version_ids)
        except Exception as e:
            _logger.error(f"Error fetching recursive dependencies for versions {version_ids}: {e}")
            return []
    @api.model
    def get_recursive_dependency_entries(self, version_ids, node_id=False, update=False):
        try:
            complete_version_ids = self.get_recursive_dependency_versions(version_ids)
            if len(complete_version_ids) < 1:
                return []

            if update and node_id:
                v_ids = list(complete_version_ids) + version_ids
                node = self.env["hp.node"].browse([node_id])
                node.write({"node_latest_ids": [(Command.SET, 0, v_ids)]})
            # '.ids' gives a list of unique IDs
            # Now get unique entry_ids for these versions using the ORM for simplicity
            versions = self.env['hp.version'].browse(list(complete_version_ids))
            entry_ids = versions.mapped('entry_id').ids
            return entry_ids
        except Exception as e:
            _logger.error(f"Error fetching recursive dependencies for versions {version_ids}: {e}")
            return []

    @api.model
    def get_entries_from_children(self, entryID):
        entry_ids = set()
        entry = self.env["hp.entry"].search([('id', '=', entryID)])[0]
        if entry:
            return self._iterative_add_entries(entry.latest_version_id)
        return None

    def _iterative_add_entries(self, version):
        stack = [version]
        entry_ids = set()

        while stack:
            current_version = stack.pop()
            if current_version.entry_id.id in entry_ids:
                continue

            entry_ids.add(version.entry_id.id)

            if current_version.child_ids:
                stack.extend(v.child_id for v in current_version.child_ids)
        if len(entry_ids) > 0:
            return entry_ids
        return None

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
            ('res_model', '=', self._name),
            ('res_id', '=', record.id),
            ('res_field', '=', field_name)
        ])
        return attachments

    @api.model
    @api.depends('entry_id', 'file_ext', 'name')
    def migrate_webdav_files_to_records(self):
        web_dav = WebDav()
        all_records = self.env[self._name].search([])

        for record in all_records:
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

    def getImageBytes(self, database1:Database, record):
        table1 = database1.get_table("hp_version")
        stmt = select(table1.c.preview_image).where(table1.c.version_id == record.id)
        return database1.execute(stmt).first()[0]

    @api.model
    @api.depends('name')
    def migrate_image_from_database(self):
        hackpdm = Database("hackpdm", "hackpdm", "hackpdm", "alderaan", 5432)
        hackpdm.start_session()
        all_records = self.env[self._name].search([])

        for record in all_records:
            attachment = record._get_attachments(record, "preview_image")
            if attachment and len(attachment)>0:
                logging.info(f"version record {attachment[0].res_id} has preview_image")
                continue

            try:
                image = self.getImageBytes(hackpdm, record)
                if image != None and image != b'':
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
    def _delete_all_records(self):
        all_records = self.env["ir.attachment"].search([("res_model", "=", "hp.version")])
        logging.info(f"deleting {len(all_records)} records linked to hp.version")
        all_records.unlink()
        logging.info(f"attachments deleted successfully")

    @api.model
    def display_all_fnames(self):
        all_records = self.env["ir.attachment"].search([("res_model", "=", "hp.version")])
        for record in all_records:
            logging.info(f"attachment id = {record.id}\nname = {record.name}\nres_model = {record.res_model}\nres_id = {record.res_id}\nres_fields = {record.res_field}\nfname = {record.store_fname}\n")

    @api.model
    def cleanup_filestore(self):
        attachment = self.env["ir.attachment"]


class hp_version_property(models.Model):
    #base fields
    _name = 'hp.version.property'
    _description = 'hp version property'
    _inherit = 'hp.common.model'

    #fields
    sw_config_name = fields.Char(string='solidworks config name')
    text_value = fields.Text(string='text value')
    number_value = fields.Float(string='number value')
    yesno_value = fields.Boolean(string='yes or no')
    date_value = fields.Datetime(
        string='time stamp',
    )
    prop_name = fields.Char(
        related='prop_id.name',
        string='property name'
    )

    #relational fields
    version_id = fields.Many2one(
        comodel_name='hp.version',
        string='version id',
    )
    prop_id = fields.Many2one(
        comodel_name='hp.property',
        string='property',
    )
    entry_id = fields.Many2one(
        comodel_name='hp.entry',
        related='version_id.entry_id',
        string='entry ID',
    )


class hp_version_relationship(models.Model):
    #base fields
    _name = 'hp.version.relationship'
    _description = 'hp version relationship'
    _inherit = 'hp.common.model'

    #fields

    #relational fields
    parent_id = fields.Many2one(
        comodel_name='hp.version',
        string='version parent',
    )
    child_id = fields.Many2one(
        comodel_name='hp.version',
        string='version child',
    )
