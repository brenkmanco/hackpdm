import logging

from odoo import fields, models, api
import datetime as dt
import numpy as np

from webdav3.client import Client
import hashlib as hash
import base64
import logging
import urllib.parse
import magic

import sqlalchemy
from sqlalchemy import MetaData, Table, create_engine, select, update
from sqlalchemy.orm import Mapped, sessionmaker, relationship

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

class hp_common_model(models.AbstractModel):
    #base fields
    _name = 'hp.common.model'
    _description = 'commmon model functions for hp models'

    @api.model
    def fast_read(self, ids, fields):
        #logging.info(f"ids {ids}\n\nfields: {fields}\n")
        if not ids or not fields:
            return []

        # sql query
        model_fields = [field for field in fields if field in self._fields]
        compute_field = [field for field in fields if field in self._fields[field].compute]
        stored_fields = [field for field in fields if field not in self._fields or not self._fields[field].compute]
        logging.info(f"\nfields: {model_fields}\n\ncompute_fields: {compute_field}\n\nstored_fields: {stored_fields}\n")

        fields_str = ', '.join(stored_fields)
        query = f"SELECT id, {fields_str} FROM {self._table} WHERE id = ANY(%s)"

        logging.info(f"\nquery: {query}")

        self.env.cr.execute(query, (ids,))
        results = self.env.cr.fetchall()
        #logging.info(f"results: {results}")

        # convert results to a list
        dict_ids = {}
        for row in results:
            all_fields = {}
            for i, field in enumerate(fields, start=1):
                all_fields[field] = row[i]

            dict_ids[str(row[0])] = all_fields

        for key, value in dict_ids.items():
            obj = self.browse(int(key))
            for field in fields:
                if field not in stored_fields:
                    value[field] = getattr(obj, field)

        #logging.info(dict_ids)
        return dict_ids

    @api.model
    def related_browse(self, ids, field_name, fields):
        if not ids:
            return []

        recordset = self.browse(ids)
        related_records = recordset.mapped(field_name)
        return related_records.read(fields)

    @api.model
    def related_search_browse(self, search, field_name, fields):
        if not search:
            return []

        recordset = self.search(search)
        related_records = recordset.mapped(field_name)
        return related_records.read(fields)

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

    def _import_versions(self, web_dav:WebDav):
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

    def _import_records(self, database1:Database, table_name:str):
        all_records = self.env[table_name.replace("_", ".")].search([])
        table1 = database1.get_table(table_name)

        for record in all_records:
            try:
                stmt = table1.select().where(table1.c.id == record.id)
                results = database1.execute(stmt)


            except Exception as e:
                logging.error(e)


            if record.name and len(attachment)>0:
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
    def _import_versions_image(self, hackpdm:Database):
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
    @api.depends('entry_id', 'file_ext', 'name')
    def migrate_webdav_files_to_records(self):
        web_dav = WebDav()
        hackpdm = Database("hackpdm", "hackpdm", "hackpdm", "alderaan", 5432)
        #odoopdm = Database("moony", "moony", "odoopdm", "10.0.0.52", 5432)
        self._import_versions(web_dav)
        hackpdm.start_session()
        self._import_versions_image(hackpdm)