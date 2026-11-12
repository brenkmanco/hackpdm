from odoo import models, fields, api
from odoo.exceptions import UserError
import logging

from . import hp_guard

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

    def init(self):
        """Creates the case-insensitive unique index on (parent_id, lower(name)) at install/upgrade.

        Odoo calls this when the module is installed or updated; it is not meant for clients. The index
        is skipped (with a warning) while existing folders still differ only by case.

        Args:
            None.

        Returns:
            None: nothing.
        """
        hp_guard.create_case_insensitive_unique_index(
            self.env.cr, self._table, "hp_directory_parent_name_ci_uniq", "parent_id")

    @api.constrains("name")
    def _check_name_path_safe(self):
        for directory in self:
            hp_guard.check_name(directory.name, "folder")

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
        """Finds the folder whose parent_path matches the given path and returns its id.

        Args:
            value (str): The folder's parent_path, the ids of the folder and its ancestors joined by
                "/" with a trailing "/" (e.g. "1/5/10/"). A " / " separated form is meant to be
                converted first, but that branch never runs (it tests ``value is str``).

        Returns:
            int: The hp.directory id.

        Raises:
            IndexError: No folder has that parent_path.
        """
        record = self._get_record_for_filepath(value, False)
        return record.id

    @api.model
    def get_dir_id_for_parentpath(self, value):
        """Finds the folder whose complete_name matches the given path and returns its id.

        Despite its name this searches complete_name ("root / Projects / Parts"), not parent_path.

        Args:
            value (str): The folder's complete_name, names joined by " / " starting at "root".

        Returns:
            int: The hp.directory id.

        Raises:
            IndexError: No folder has that complete_name.
        """
        record = self._get_record_for_filepath(value, True)
        return record.id

    @api.model
    def get_directories(self, value=None):
        """Placeholder kept for older clients; does nothing.

        Args:
            value (Any, optional): Ignored. Defaults to None.

        Returns:
            None: always.
        """
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
        """Returns a folder's whole sub-tree, with entries, found by its path.

        Not usable as written: it calls ``_get_record_for_filepath(value)`` without the required
        ``is_parent_path`` argument, so every call raises TypeError. The client uses
        :meth:`get_children_directories_by_id` instead.

        Args:
            value (str): The folder's path.

        Returns:
            dict | None: The tree as built by ``_recurse_directories``, or None when no folder matches.

        Raises:
            TypeError: Always (see above).
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
        """Returns a folder and everything below it as a nested dictionary.

        Each level is ``{"id": int, "name": str, "directories": {name: <level>}, "entries": {name: info}}``.
        Entries (the same fields as :meth:`get_entries`, without "type") are filled in only when
        ``withEntries`` is true; deleted entries are left out.

        Args:
            value (int): The hp.directory id of the top folder.
            withEntries (bool): Whether to list each folder's entries as well as its sub-folders.

        Returns:
            dict: The nested tree, or ``{}`` when the folder does not exist.

        Raises:
            IndexError: No folder has that id (the search result is indexed before it is checked).
        """
        record = self.env["hp.directory"].search([('id', '=', value)])[0]
        directory_dict = {}
        if record:
            directory_dict = self._recurse_directories(record, withEntries)
            #print(dict_ids)

        return directory_dict

    @api.model
    def get_dir_ids_for_filepaths(self, values):
        """Resolves several parent_path strings to folder ids, one :meth:`get_dir_id_for_filepath` each.

        Args:
            values (list[str]): parent_path strings ("1/5/10/").

        Returns:
            list[int]: The hp.directory ids, in the same order.

        Raises:
            IndexError: One of the paths matches no folder.
        """
        list_ids = []
        for value in values:
            list_ids.append(self.get_dir_id_for_filepath(value))
        return list_ids

    @api.model
    def get_entries(self, value):
        """Lists the files directly in one folder, keyed by file name, for the file manager.

        Each value is a dict with: id (int), type (str, the file extension), checkout (int, the user id
        holding it or 0), category (str), fullname (str, "<folder complete_name> / <name>"), size (int),
        latest (int, latest version id), release (int, latest release id or 0), latest_checksum (str),
        latest_date (datetime) and deleted (bool).

        Args:
            value (list): A two-item list ``[directory_id (int), show_deleted (bool)]``; deleted files
                are included only when show_deleted is true.

        Returns:
            dict[str, dict]: File name to entry info; ``{}`` when the folder does not exist or anything
            fails (the error is logged, not raised).
        """

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
        """Returns the ids of the files in a folder, optionally with every sub-folder's files.

        This is what the client's folder ("tree") actions use to find the files they apply to.

        Args:
            value (int): The hp.directory id.
            withDeleted (bool, optional): Include files marked deleted. Defaults to False.
            withSubEntries (bool, optional): Include the files of all sub-folders, recursively.
                Defaults to True.

        Returns:
            list[int]: hp.entry ids; ``[]`` when the folder does not exist or anything fails (logged).
        """
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
    def _recurse_directories_finding(self, directory, paths:list[str], index:int, forClient:bool, directories:list):
        directories.append(directory.id)
        if (index < len(paths) - 1):
            wanted = paths[index + 1]
            children = directory.child_ids
            # the exact name first; otherwise the same name in another case, which is the same
            # folder on Windows (and creating it again would break the case-insensitive unique index)
            match = children.filtered(lambda d: d.name == wanted)[:1] \
                or children.filtered(lambda d: (d.name or "").casefold() == (wanted or "").casefold())[:1]
            if match:
                return self._recurse_directories_finding(match, paths, index + 1, forClient, directories)

        # if there are no children inside of directory equal to the name
        # in paths[index] then the previous index
        # or index is greater than or equal to the count of paths in paths
        if (forClient):
            return {
                'index': index,
                'dir_id': directory.id,
                'known': directories,
            }
        else:
            return {
                'index': index,
                'dir_id': directory.id,
                'known': directories,
            }

    # pathway will have a path that needs to be parsed to find the last available directory
    # and then return a dictionary with the index of the path where it was last available
    # and the ID of the last last available dir_id
    @api.model
    def last_available_directory(self, paths):
        """Walks a folder path from the root and reports how far it already exists in the vault.

        The client calls this before a commit to learn which folders it still has to create. Names
        are matched exactly first, then ignoring case (the same folder on Windows).

        Args:
            paths (list[str] | list[list[str]]): Folder names from the vault root down, e.g.
                ``["pwa", "Projects", "Parts"]``; the first name is replaced by "root". A list wrapped
                in another list (how some RPC callers send it) is unwrapped.

        Returns:
            dict: ``{"index": int, "dir_id": int, "known": list[int]}`` — the index in ``paths`` of
            the deepest folder that exists, that folder's id, and the ids of every folder found
            along the way (root first).

        Raises:
            UserError: ``paths`` is empty.
        """
        last_dir = {}
        # if the first array element is pwa change it to root and
        # then if the first array element doesn't equal root then
        # the beginning of the path isn't going to be found
        if paths and type(paths[0]) == list:
            paths = paths[0]
        if not paths:
            raise UserError("Expected a path of folder names, got an empty one.")
        paths[0] = "root"

        records = self.env["hp.directory"].search([("id", "=", 1)])
        root = records[0]
        last_dir = self._recurse_directories_finding(root, paths, 0, True, [])
        return last_dir

    @api.model
    def create_new_details(self, paths):
        """Unfinished: walks the path like :meth:`last_available_directory` but returns nothing.

        Args:
            paths (list[str] | list[list[str]]): Folder names from the vault root down.

        Returns:
            None: always (the walk result is discarded).

        Raises:
            UserError: ``paths`` is empty.
        """
        details = {}
        
        if paths and type(paths[0]) == list:
            paths = paths[0]
        if not paths:
            raise UserError("Expected a path of folder names, got an empty one.")
        paths[0] = "root"

        records = self.env["hp.directory"].search([("id", "=", 1)])
        root = records[0]
        details = self._recurse_directories_finding(root, paths, 0, False, [])


    @api.model_create_multi
    def create_new_directories(self):
        """Placeholder; does nothing.

        Args:
            None.

        Returns:
            None: always.
        """
        pass

# file name, file path, checksum, attachment_id (for latest version)
# on hack loop through and download to directory
