from odoo import models, fields, api
import logging

class hp_directory(models.Model):
    _name = 'hp.directory'
    _description = 'directory'
    _parent_name = 'parent_id'
    _parent_store = True
    _rec_name = 'complete_name'
    _order = 'complete_name'
    _inherit = 'hp.common.model'
    _sql_constraints = [
        ('child_name_uniq', 'unique (parent_id, name)', 'Child directory names must be unique')
    ]

    name = fields.Char(
        required=True,
        string='directory name',
    )
    parent_path = fields.Char(
        index=True,
        unaccent=False,
    )
    complete_name = fields.Char(
        string='complete name',
        compute='_compute_complete_name',
        recursive=True,
        store=True,
    )
    full_path = fields.Char(
        string='full path',
        compute='_compute_complete_name',
        recursive=True,
        store=True,
    )

    sandboxed = fields.Boolean(
        string='sandboxed',
    )
    deleted = fields.Boolean(
        default=False,
        string='deleted',
    )

    parent_id = fields.Many2one(
        comodel_name='hp.directory',
        string='parent directory',
    )
    default_cat = fields.Many2one(
        comodel_name='hp.category',
        string='default category',
    )

    child_ids = fields.One2many(
        comodel_name='hp.directory',
        inverse_name='parent_id',
        string='child directories',
    )
    entry_ids = fields.One2many(
        comodel_name="hp.entry",
        inverse_name='dir_id',
        string='entries',
    )

    # parent_path_temp = fields.Char(
    #     compute='_compute_parent_path',
    #     recursive=True,
    #     store=True,
    # )
    # @api.depends('parent_id')
    # def _compute_parent_path(self):
    #     for directory in self:
    #         if directory.parent_id:
    #             directory.parent_path_temp = f'{directory.parent_id.id}/{directory.id}/'
    #         else:
    #             directory.parent_path_temp = f'{directory.id}/'

    @api.depends('name', 'parent_id', 'parent_id.complete_name')
    def _compute_complete_name(self):
        for directory in self:
            if directory.parent_id:
                directory.complete_name = f'{directory.parent_id.complete_name} / {directory.name}'
                directory.full_path = f'{directory.parent_id.complete_name}/"{directory.name}"'
            else:
                directory.complete_name = directory.name
                directory.full_path = directory.name

    @api.model
    def _get_record_for_filepath(self, value, is_parent_path):
        """ the client will pass a filepath here and then search for the filepath
            that is applicable and return the record that it matches
            is_parent_path indicates it is trying to find it via the parent_path
            false means it'll try to find it by the complete_name
        """
        if is_parent_path:
            if value is str:
                split_directories = value.split(' / ')
                split_directories[0] = "root"
                # should look like 'root / Categories'
                value = " / ".join(split_directories)
            record = self.env['hp.directory'].search([('complete_name', '=', value)])[0]
        else:
            if value is str:
                split_directories = value.split(' / ')
                if split_directories[0] == "0" or split_directories[0] == "":
                    split_directories = split_directories[1:]

                # should look like '1/5/10/
                value = "/".join(split_directories) + "/"
            record = self.env['hp.directory'].search([('parent_path', '=', value)])[0]
        return record

    @api.model
    def get_dir_id_for_filepath(self, value):
        """ the client will pass a filepath here and then search for the filepath
            that is applicable and return the dir_id that it matches
        """
        record = self._get_record_for_filepath(value, False)
        return record.id

    @api.model
    def get_dir_id_for_parentpath(self, value):
        """ the client will pass a filepath here and then search for the filepath
            that is applicable and return the dir_id that it matches
        """
        record = self._get_record_for_filepath(value, True)
        return record.id

    @api.model
    def get_directories(self, value=None):
        pass

    #logging.basicConfig(level=logging.DEBUG)

    @api.model
    def _get_entry_dict(self, directory, withDetails, showInactive=False):
        child_entries = {}
        entry_dict = {}
        for entry in directory.entry_ids:
            if not(showInactive) and entry["deleted"] == True:
                continue

            entry_dict = {}
            entry_dict["id"] = entry.id
            if withDetails:
                entry_dict["type"] = entry.type_id.file_ext

            myId:int = 0
            if entry.checkout_user.id:
                myId = entry.checkout_user.id

            entry_dict["checkout"] = myId
            entry_dict["category"] = entry.cat_id.name
            entry_dict["fullname"] = f'{directory.complete_name} / {entry.name}'
            #entry_dict["latest"] = entry.latest_version_id
            entry_dict["size"] = entry.latest_file_size
            latest_version = entry.latest_version_id
            entry_dict["latest"] = latest_version.id
            latest_release = entry.latest_release_id
            entry_dict["release"] = latest_release.id
            entry_dict["latest_checksum"] = latest_version.checksum
            entry_dict["latest_date"] = latest_version.file_modify_stamp
            entry_dict["deleted"] = entry.deleted

            child_entries[entry.name] = entry_dict
        return child_entries

    @api.model
    @api.depends('name')
    def _recurse_directories(self, directory, withEntries:bool):
        directory_dict = {}
        # get id and name for directory
        directory_dict["id"] = directory.id
        directory_dict["name"] = directory.name

        # get all children directories within directory

        child_directories = {}
        for child_id in directory.child_ids:
            child_directories[child_id.name] = self._recurse_directories(child_id, withEntries)

        directory_dict["directories"] = child_directories

        # get all entries within directory
        directory_dict["entries"] = {}
        if withEntries:
            directory_dict["entries"] = self._get_entry_dict(directory, False)

        #logging.info(f'\n{dict_ids}\n')
        return directory_dict

    @api.model
    def get_children_directories(self, value):
        """ the client passes a filepath then get all children directory id's relating to
            this directory id

        """
        # import pdb
        # pdb.set_trace()
        record = self._get_record_for_filepath(value)
        if record:
            dict_ids = self._recurse_directories(record, True)
            #print(dict_ids)
            return dict_ids
        else:
            return None

    @api.model
    def get_children_directories_by_id(self, value, withEntries):
        record = self.env["hp.directory"].search([('id', '=', value)])[0]
        directory_dict = {}
        if record:
            directory_dict = self._recurse_directories(record, withEntries)
            #print(dict_ids)

        return directory_dict

    @api.model
    def get_dir_ids_for_filepaths(self, values):
        list_ids = []
        for value in values:
            list_ids.append(self.get_dir_id_for_filepath(value))
        return list_ids

    @api.model
    def get_entries(self, value):

        showInactive = value[1]
        value = value[0]

        entry_dict = {}
        try:
            record = self.env["hp.directory"].search([('id', '=', value)])[0]

            if record:
                entry_dict = self._get_entry_dict(record, True, showInactive)
                #print(dict_ids)
        except Exception as e:
            logging.error(e)
        return entry_dict

    def _get_all_entry_ids(self, directory, root_entry_list, withDeleted=False):
        for entry in directory.entry_ids:
            if withDeleted or not(entry.deleted):
                root_entry_list.append(entry.id)
        for children_directory in directory.child_ids:
            self._get_all_entry_ids(children_directory, root_entry_list, withDeleted)

    @api.model
    def get_all_entry_ids(self, value, withDeleted=False, withSubEntries=True):
        entry_list = []
        try:
            record = self.env["hp.directory"].search([('id', '=', value)])[0]
            if record:
                if withSubEntries:
                    self._get_all_entry_ids(record, entry_list, withDeleted)
                else:
                    for entry in record.entry_ids:
                        if withDeleted or not(entry.deleted):
                            entry_list.append(entry.id)

        except Exception as e:
            logging.error(e)
        return entry_list

    @api.model
    def _recurse_directories_finding(self, directory, paths:list[str], index:int, forClient:bool):

        if (index < len(paths) - 1):
            for dir in directory.child_ids:
                if (paths[index + 1] == dir.name):
                    return self._recurse_directories_finding(dir, paths, index + 1, forClient)

        # if there are no children inside of directory equal to the name
        # in paths[index] then the previous index
        # or index is greater than or equal to the count of paths in paths
        if (forClient):
            return {
                'index': index,
                'dir_id': directory.id,
            }
        else:
            return {
                'index': index,
                'directory': directory,
            }



    # pathway will have a path that needs to be parsed to find the last available directory
    # and then return a dictionary with the index of the path where it was last available
    # and the ID of the last last available dir_id
    @api.model
    def last_available_directory(self, paths):
        last_dir = {}
        # if the first array element is pwa change it to root and
        # then if the first array element doesn't equal root then
        # the beginning of the path isn't going to be found
        paths[0] == "root"

        records = self.env["hp.directory"].search([("id", "=", 1)])
        root = records[0]
        last_dir = self._recurse_directories_finding(root, paths, 0, True)
        return last_dir

    @api.model
    def create_new_details(self, paths):
        details = {}
        paths[0] = "root"

        records = self.env["hp.directory"].search([("id", "=", 1)])
        root = records[0]
        details = self._recurse_directories_finding(root, paths, 0, False)


    @api.model_create_multi
    def create_new_directories(self):
        pass

# file name, file path, checksum, attachment_id (for latest version)
# on hack loop through and download to directory
