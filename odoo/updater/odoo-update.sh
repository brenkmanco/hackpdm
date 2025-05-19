#!/bin/bash

#odoopdmquery="psql -U moony -d odoopdm -a -f "

IS_HpDirectory="INSERT INTO hp_directory (id, parent_id, name, default_cat, create_date, create_uid, write_date, write_uid, sandboxed, deleted) values "
IS_HpEntry="INSERT INTO hp_entry (id, dir_id, name, type_id, create_date, create_uid, checkout_user, checkout_date, checkout_node, deleted) values "
IS_HpRelease="INSERT INTO hp_release (id, release_user_id, write_date, release_note) values "
IS_HpVersion="INSERT INTO hp_version (id, entry_id, file_modify_stamp, create_date, create_uid, node_id) values "
IS_HpVersionProperty="INSERT INTO hp_version_property (id, sw_config_name, prop_id, text_value, date_value, number_value, yesno_value) values "
IS_HpNode="INSERT INTO hp_node (id, name, create_uid, create_date) values "
IS_HpEntryNameFilter="INSERT INTO hp_entry_name_filter (id, name_proto, name_regex, description) values "
IS_HpProperty="INSERT INTO hp_property (id, name, prop_type, active, create_date, create_uid) values "
IS_HpType="INSERT INTO hp_type (id, cat_id, file_ext, type_regex, description) values "
IS_HpVersionRelationship="TRUNCATE hp_version_relationship; 
INSERT INTO hp_version_relationship (parent_id, child_id) values "
IS_HpReleaseVersionRel="TRUNCATE hp_release_version_rel; 
INSERT INTO hp_release_version_rel (release_id, release_version, create_uid, create_date) values "

directory(){
    echo $IS_HpDirectory > directory.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> directory.sql << EOL
select
      '(' || dir_id  || ', '
      || coalesce(parent_id::varchar, 'NULL') || ', '
      || '''' || replace(dir_name, '''', '''''') || ''', '
      || default_cat || ', '
      || '''' || create_stamp || ''', '
      || um.odoo_user_id || ', '
      || '''' || modify_stamp || ''', '
      || um.odoo_user_id || ', '
      || sandboxed || ', '
      || CASE WHEN active=true THEN false ELSE true END || '),'
from hp_directory hd
left join user_map um on um.user_id=hd.create_user
order by dir_id, parent_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' directory.sql
    psql -U moony -d odoopdm -a -f directory.sql
}

entry(){
    echo $IS_HpEntry > entry.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> entry.sql << EOL
select
      '(' || entry_id  || ', '
      || dir_id || ', '
      || '''' || replace(entry_name, '''', '''''') || ''', '
      || type_id || ', '
      || '''' || create_stamp || ''', '
      || um.odoo_user_id || ', '
      || coalesce(cast(umb.odoo_user_id as text), 'NULL') || ', '
      || '''' || coalesce(
                                      case
                                              when checkout_date is NULL then 'NULL'
                                              else cast(checkout_date as text)
                                      end,
                                      'NULL'
                              ) || ''', '
      || coalesce(cast(checkout_node as text), 'NULL') || ', '
      || CASE WHEN active=true THEN false ELSE true END || '),'
from hp_entry he
left join user_map umb on umb.user_id=he.checkout_user
left join user_map um on um.user_id=he.create_user
order by entry_id, dir_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' entry.sql
    sed -i "s/'NULL'/NULL/g" entry.sql
    psql -U moony -d odoopdm -a -f entry.sql
}

release(){
    echo $IS_HpRelease > release.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> release.sql << EOL
select
      '(' || release_id  || ', '
      || umb.odoo_user_id || ', '
      || '''' || coalesce(
                                      case
                                              when release_stamp is NULL then 'NULL'
                                              else cast(release_stamp as text)
                                      end,
                                      'NULL'
                              ) || ''', '
      || '''' || coalesce(cast(release_note as text), 'NULL') || '''),'
from hp_release he
left join user_map umb on umb.user_id=he.release_user
order by release_id, release_user;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' release.sql
    sed -i "s/'NULL'/NULL/g" release.sql
    psql -U moony -d odoopdm -a -f release.sql
}

version(){
    echo $IS_HpVersion > version.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> version.sql << EOL
select
      '(' || version_id  || ', '
      || entry_id || ', '
      --|| file_size || ', ' -- related to attachment_ids
      || '''' || coalesce(cast(file_modify_stamp as text), 'NULL') || ''', '
      || '''' || coalesce(cast(create_stamp as text), 'NULL') || ''', '
      || umb.odoo_user_id || ', '
      --|| coalesce(md5sum, 'NULL') || ', ' -- computed field
      --|| coalesce(cast(preview_image as text), 'NULL' || ', ' -- stored elsewhere
      || create_node || '),'
from hp_version he
left join user_map umb on umb.user_id=he.create_user
order by version_id, entry_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' version.sql
    psql -U moony -d odoopdm -a -f version.sql
}

vers_prop(){
    echo $IS_HpVersionProperty > vers_prop.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> vers_prop.sql << EOL
select
      '(' || version_id  || ', '
      || '''' || replace(config_name, '''', '''''') || ''', '
      || prop_id || ', '
      || '''' || replace(coalesce(cast(text_value as text), 'NULL'), '''', '''''') || ''', '
      || '''' || coalesce(cast(date_value as text), 'NULL') || ''', '
      || coalesce(cast(number_value as text), 'NULL') || ', '
      || coalesce(cast(yesno_value as text), 'NULL') || '),'
from hp_version_property he
order by version_id, config_name;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' vers_prop.sql
    sed -i "s/'NULL'/NULL/g" vers_prop.sql
    psql -U moony -d odoopdm -a -f vers_prop.sql
}

vers_relationship(){
    echo $IS_HpVersionRelationship > vers_relationship.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> vers_relationship.sql << EOL
select
    '(' || rel_parent_id  || ', '
    || rel_child_id || '),'
from hp_version_relationship he
order by rel_parent_id, rel_child_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1;/' vers_relationship.sql
    sed -i "s/'NULL'/NULL/g" vers_relationship.sql
    psql -U moony -d odoopdm -a -f vers_relationship.sql
}

vers_rel(){
    echo $IS_HpReleaseVersionRel > vers_rel.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> vers_rel.sql << EOL
select
    '(' || rel_release_id  || ', '
    || rel_version_id || ', '
    || umb.odoo_user_id || ', '
    || '''' || coalesce(cast(rel_ver_stamp as text), 'NULL') || '''),'
from hp_release_version_rel he
left join user_map umb on umb.user_id=he.rel_ver_user
order by rel_release_id, rel_version_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1;/' vers_rel.sql
    sed -i "s/'NULL'/NULL/g" vers_rel.sql
    psql -U moony -d odoopdm -a -f vers_rel.sql
}

node(){
    echo $IS_HpNode > node.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> node.sql << EOL
select
    '(' || node_id  || ', '
    || '''' || replace(node_name, '''', '''''') || ''', '
    || umb.odoo_user_id || ', '
    || '''' || coalesce(cast(create_stamp as text), 'NULL') || '''),'
from hp_node he
left join user_map umb on umb.user_id=he.create_user
order by node_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' node.sql
    sed -i "s/'NULL'/NULL/g" node.sql
    psql -U moony -d odoopdm -a -f node.sql
}

entry_name_filter(){
    echo $IS_HpEntryNameFilter > entry_name_filter.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> entry_name_filter.sql << EOL
select
    '(' || filter_id  || ', '
    || '''' || replace(name_proto, '''', '''''') || ''', '
    || '''' || replace(name_regex, '''', '''''') || ''', '
    || '''' || replace(description, '''', '''''') || '''), '
from hp_entry_name_filter he
order by filter_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' entry_name_filter.sql
    sed -i "s/'NULL'/NULL/g" entry_name_filter.sql
    psql -U moony -d odoopdm -a -f entry_name_filter.sql
}


#id, name, prop_type, active, create_date, create_uid
property(){
    echo $IS_HpProperty > property.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> property.sql << EOL
select
    '(' || prop_id  || ', '
    || '''' || replace(prop_name, '''', '''''') || ''', '
    || '''' || replace(prop_type, '''', '''''') || ''', '
    || active || ', '
    || '''' || coalesce(cast(create_stamp as text), 'NULL') || ''', '
    || umb.odoo_user_id || '), '
from hp_property he
left join user_map umb on umb.user_id=he.create_user
order by prop_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' property.sql
    sed -i "s/'NULL'/NULL/g" property.sql
    psql -U moony -d odoopdm -a -f property.sql
}

#id, cat_id, file_ext, type_regex, description
type(){
    echo $IS_HpType > type.sql
    cat | psql -h alderaan -U hackpdm -W -t hackpdm >> type.sql << EOL
select
    '(' || type_id  || ', '
    || default_cat || ', '
    || '''' || replace(file_ext, '''', '''''') || ''', '
    || '''' || replace(type_regex, '''', '''''') || ''', '
    || '''' || replace(description, '''', '''''') || '''), '
from hp_type he
order by type_id;
EOL
    sed -i -e 'N;$!P;$!D; s/\(.*\),/\1 on conflict (id) do nothing;/' type.sql
    sed -i "s/'NULL'/NULL/g" type.sql
    psql -U moony -d odoopdm -a -f type.sql
}

#update different tables
type
property
node
entry_name_filter

directory
entry
vers_prop
version
release

#doesn't have a corresponding id so the table would need to be erased and then insert the values
vers_relationship
vers_rel