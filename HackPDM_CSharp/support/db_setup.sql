﻿



-- -----------------------------------------------------------------------------

--DROP FUNCTION fcn_common_modify_stamp();

CREATE OR REPLACE FUNCTION fcn_common_modify_stamp() RETURNS trigger AS
$BODY$
    BEGIN
        NEW.modify_stamp := 'now()';
        RETURN NEW;
    END;
$BODY$
  LANGUAGE plpgsql VOLATILE
  COST 100;




-- -----------------------------------------------------------------------------

--drop table hp_user;
--drop sequence seq_hp_user_user_id;

CREATE SEQUENCE seq_hp_user_user_id START 1001;

create table hp_user (
	
	user_id integer NOT NULL default nextval('seq_hp_user_user_id'::regclass),
	login_name varchar(100) NOT NULL,
	last_name varchar(100) NOT NULL,
	first_name varchar(100) NOT NULL,
	email varchar(100),
	passwd varchar(100),
	modify_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	modify_user integer NOT NULL,
	
	primary key (user_id),
	foreign key (modify_user) references hp_user (user_id)
	
);

CREATE TRIGGER trg_hp_user_1_modify_stamp
  BEFORE UPDATE
  ON hp_user
  FOR EACH ROW
  EXECUTE PROCEDURE fcn_common_modify_stamp();


insert into hp_user (user_id,login_name,last_name,first_name,modify_user,passwd) values (0,'admin','User','Admin',0,'admin');
insert into hp_user (user_id,login_name,last_name,first_name,modify_user,passwd) values (1001,'demo','User','Demo',0,'demo');




-- -----------------------------------------------------------------------------


--drop table hp_node;
--drop sequence seq_hp_node_node_id;

CREATE SEQUENCE seq_hp_node_node_id START 1001;
CREATE TABLE hp_node (
	
	node_id integer NOT NULL default nextval('seq_hp_node_node_id'::regclass),
	node_name varchar(100) NOT NULL UNIQUE,
	create_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	create_user integer NOT NULL,
	
	primary key (node_id),
	foreign key (create_user) references hp_user (user_id)
	
);


insert into hp_node (node_name,create_user) values ('Jerry-HP',0);




-- -----------------------------------------------------------------------------


--drop table hp_category;
--drop sequence seq_hp_category_cat_id;

CREATE SEQUENCE seq_hp_category_cat_id START 1001;
create table hp_category (
	
	cat_id integer NOT NULL default nextval('seq_hp_category_cat_id'::regclass),
	cat_name varchar(25) NOT NULL,
	cat_description varchar(255) NOT NULL,
	track_version boolean NOT NULL,
	track_depends boolean NOT NULL,
	create_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	create_user integer NOT NULL,
	
	primary key (cat_id),
	foreign key (create_user) references hp_user (user_id)
	
);

insert into hp_category (cat_id,cat_name,cat_description,track_version,track_depends,create_user) values (1,'CAD','CAD files are versioned and have dependencies.',true,true,0);
insert into hp_category (cat_id,cat_name,cat_description,track_version,track_depends,create_user) values (2,'Library','Typically CAD files, however no versions are kept.  The files may have dependencies.',false,true,0);
insert into hp_category (cat_id,cat_name,cat_description,track_version,track_depends,create_user) values (3,'Static','No dependency tracking.  No versioning.',false,false,0);
insert into hp_category (cat_id,cat_name,cat_description,track_version,track_depends,create_user) values (4,'Documents','Keep file versions, but no dependency tracking.',true,false,0);




-- -----------------------------------------------------------------------------


--drop table hp_directory;
--drop sequence seq_hp_directory_dir_id;

CREATE SEQUENCE seq_hp_directory_dir_id START 1001;
CREATE TABLE hp_directory (
	
	dir_id integer NOT NULL default nextval('seq_hp_directory_dir_id'::regclass),
	parent_id integer CHECK (parent_id IS NOT NULL or dir_id=1::integer),
	dir_name character varying(255),
	default_cat integer NOT NULL,
	create_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	create_user integer NOT NULL,
	modify_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	modify_user integer NOT NULL,
	
	primary key (dir_id),
	foreign key (parent_id) references hp_directory (dir_id),
	foreign key (default_cat) references hp_category (cat_id),
	foreign key (create_user) references hp_user (user_id),
	foreign key (modify_user) references hp_user (user_id),
	unique(dir_id,parent_id)
	
);

CREATE TRIGGER trg_hp_directory_1_modify_stamp
  BEFORE UPDATE
  ON hp_directory
  FOR EACH ROW
  EXECUTE PROCEDURE fcn_common_modify_stamp();

CREATE UNIQUE INDEX ON hp_directory (parent_id, lower(dir_name::text));

insert into hp_directory (dir_id,parent_id,dir_name,default_cat,create_user,modify_user) values (1,NULL,'root',1,0,0);
-- insert into hp_directory (parent_id,dir_name,default_cat,create_user,modify_user) values (0,'1',1,0,0);
-- insert into hp_directory (parent_id,dir_name,default_cat,create_user,modify_user) values (1001,'1.1',1,0,0);
-- insert into hp_directory (parent_id,dir_name,default_cat,create_user,modify_user) values (1001,'1.2',1,0,0);
-- insert into hp_directory (parent_id,dir_name,default_cat,create_user,modify_user) values (1002,'2.1',1,0,0);

/* upgrade
ALTER TABLE hp_directory drop CONSTRAINT hp_directory_check;
insert into hp_directory (dir_id,parent_id,dir_name,default_cat,create_user,modify_user) values (1,NULL,'root',1,0,0);
update hp_directory set parent_id=1 where parent_id=0;
delete from hp_directory where dir_id=0;
ALTER TABLE hp_directory add CONSTRAINT hp_directory_check CHECK (parent_id IS NOT NULL OR dir_id = 1);
*/

/*
	
	select
		dir_id,
		parent_id,
		dir_name
		create_stamp,
		create_user,
		modify_stamp,
		modify_user,
		true as is_remote
	from hp_directory
	order by parent_id,dir_id;
	
	
	with recursive dir_tree(dir_id,parent_id) as (
			select
				dir_id,
				parent_id
			from hp_directory
			where parent_id is null
		union all
			select
				d.dir_id,
				d.parent_id
			from dir_tree as p, hp_directory as d
			where d.parent_id=p.dir_id
	)
	select * from dir_tree
	
*/




-- -----------------------------------------------------------------------------

--drop table hp_type;
--drop sequence seq_hp_type_type_id;

CREATE SEQUENCE seq_hp_type_type_id START 1001;
create table hp_type (
	
	type_id integer NOT NULL default nextval('seq_hp_type_type_id'::regclass),
	file_ext varchar(25) NOT NULL,
	default_cat integer NOT NULL,
	icon bytea NOT NULL,
	type_regex varchar(25) NOT NULL,
	description varchar(255) NOT NULL,
	
	primary key (type_id),
	foreign key (default_cat) references hp_category (cat_id)
	
);

CREATE UNIQUE INDEX ON hp_type (lower(file_ext::text));

-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (1,'SLDPRT',1,'\.(SLDPRT)$','SolidWorks Part File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (2,'SLDASM',1,'\.(SLDASM)$','SolidWorks Assembly File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (3,'SLDDRW',1,'\.(SLDDRW)$','SolidWorks Drawing File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (4,'sldmat',2,'\.(sldmat)$','SolidWorks Material Definition File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (5,'SLDLFP',2,'\.(SLDLFP)$','SolidWorks File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (6,'png',4,'\.(png)$','PNG Image File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (7,'jpg',4,'\.(jpg)$','JPG Image File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (8,'mdb',4,'\.(mdb)$','MS Access Database File');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (9,'doc',4,'\.(doc)$','MS Word 97/2003');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (10,'docx',4,'\.(docx)$','MS Word 2007+');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (11,'xls',4,'\.(xls)$','MS Excel 97/2003');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (12,'xlsx',4,'\.(xls)x$','MS Excel 2007+');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (13,'pdf',4,'\.(pdf)$','Adobe PDF Document');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (14,'prt.1',4,'\.(prt\.[0-9]+)$','Pro/Engineer Part');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (15,'asm.1',4,'\.(asm\.[0-9]+)$','Pro/Engineer Assembly');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (15,'drw.1',4,'\.(drw\.[0-9]+)$','Pro/Engineer Drawing');
-- insert into hp_type (type_id,file_ext,default_cat,type_regex,description) values (15,'txt.1',4,'\.(txt\.[0-9]+)$','Pro/Engineer Trail/Log File');




-- -----------------------------------------------------------------------------

--drop table hp_entry_name_filter;
--drop sequence seq_hp_entry_name_filter_id;

CREATE SEQUENCE seq_hp_entry_name_filter_id START 1001;
create table hp_entry_name_filter (
	
	filter_id integer NOT NULL default nextval('seq_hp_entry_name_filter_id'::regclass),
	name_proto varchar(255) NOT NULL,
	name_regex varchar(255) NOT NULL,
	description varchar(255) NOT NULL,
	
	primary key (filter_id)
	
);

CREATE UNIQUE INDEX ON hp_entry_name_filter (lower(name_proto::text));

insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.aaa~', '\.(.+~)$', 'Temporary Backup File');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.msi', '\.(msi)$', 'Microsoft Installer');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.dll', '\.(dll)$', 'Microsoft Dynamic Linked Library');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.exe', '\.(exe)$', 'Microsoft Executable');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.bak', '\.(bak)$', 'AutoCAD Drawing Backup File');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.db', '^.+\.(db)$', 'Windows Display Settings File');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.dropbox', '^.+\.(dropbox)$', 'Dropbox Settings');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.hold', '^.+\.(hold)$', 'Held File');
insert into hp_entry_name_filter (name_proto,name_regex,description) values ('.old', '^.+\.(old)$', 'Old Copy of Any File');




-- -----------------------------------------------------------------------------

--drop table hp_entry;
--drop sequence seq_hp_entry_entry_id;

CREATE SEQUENCE seq_hp_entry_entry_id START 1001;
create table hp_entry (
	
	entry_id integer NOT NULL default nextval('seq_hp_entry_entry_id'::regclass),
	dir_id integer NOT NULL,
	entry_name varchar(255) NOT NULL,
	type_id integer NOT NULL,
	cat_id integer NOT NULL,
	create_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	create_user integer NOT NULL,
	checkout_user integer,
	checkout_date timestamp(6) without time zone,
	checkout_node integer,
	
	primary key (entry_id),
	foreign key (dir_id) references hp_directory (dir_id),
	foreign key (type_id) references hp_type (type_id),
	foreign key (cat_id) references hp_category (cat_id),
	foreign key (create_user) references hp_user (user_id),
	foreign key (checkout_user) references hp_user (user_id),
	foreign key (checkout_node) references hp_node (node_id)
	
);

CREATE UNIQUE INDEX ON hp_entry (dir_id, lower(entry_name::text));




-- -----------------------------------------------------------------------------


--drop table hp_version;
--drop sequence seq_hp_version_version_id;

CREATE SEQUENCE seq_hp_version_version_id START 1001;
create table hp_version (
	
	version_id integer NOT NULL default nextval('seq_hp_version_version_id'::regclass),
	entry_id integer NOT NULL,
	file_size bigint NOT NULL,
	file_modify_stamp timestamp(6) without time zone NOT NULL,
	create_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	create_user integer NOT NULL,
	blob_ref oid NOT NULL,
	md5sum text NOT NULL,
	preview_image bytea NOT NULL,
	release_user integer,
	release_date timestamp(6) without time zone,
	release_tag varchar(255),
	
	primary key (version_id),
	foreign key (entry_id) references hp_entry (entry_id),
	foreign key (create_user) references hp_user (user_id),
	foreign key (release_user) references hp_user (user_id)
	
);




-- -----------------------------------------------------------------------------


--drop table hp_property;
--drop sequence seq_hp_property_prop_id;

CREATE SEQUENCE seq_hp_property_prop_id START 1001;
create table hp_property (
	
	prop_id integer NOT NULL default nextval('seq_hp_property_prop_id'::regclass),
	prop_name varchar(255) NOT NULL,
	prop_type varchar(10) NOT NULL CHECK (prop_type='text' or prop_type='date' or prop_type='number' or prop_type='yesno'),
	create_stamp timestamp(6) without time zone NOT NULL DEFAULT now(),
	create_user integer NOT NULL,
	
	primary key (prop_id),
	foreign key(create_user) references hp_user(user_id)
	
);




-- -----------------------------------------------------------------------------


--drop table hp_version_property;

create table hp_version_property (
	
	version_id integer NOT NULL,
	config_name varchar NOT NULL,
	prop_id integer NOT NULL,
	text_value text,
	date_value timestamp(6) without time zone,
	number_value decimal,
	yesno_value boolean,
	
	primary key (version_id,config_name,prop_id),
	foreign key (version_id) references hp_version (version_id),
	foreign key (prop_id) references hp_property (prop_id)
	
);

CREATE UNIQUE INDEX ON hp_version_property (lower(config_name::text));




-- -----------------------------------------------------------------------------


--drop table hp_category_property;

create table hp_category_property (
	
	cat_id integer NOT NULL,
	prop_id integer NOT NULL,
	require boolean,
	
	primary key (cat_id,prop_id),
	foreign key (cat_id) references hp_category (cat_id),
	foreign key (prop_id) references hp_property (prop_id)
	
);




-- -----------------------------------------------------------------------------


--drop table hp_version_relationship;

create table hp_version_relationship (
	
	rel_parent_id integer,
	rel_child_id integer,
	
	primary key (rel_parent_id, rel_child_id),
	foreign key(rel_parent_id) references hp_version(version_id),
	foreign key(rel_child_id) references hp_version(version_id)
	
);




-- -----------------------------------------------------------------------------

-- DROP FUNCTION fcn_dependency_recursive(integer[]);

CREATE OR REPLACE FUNCTION fcn_dependency_recursive(IN parent_id_array integer[], OUT rel_parent_id integer, OUT rel_child_id integer)
  RETURNS SETOF record AS
$BODY$
	WITH RECURSIVE child_versions (rel_parent_id, rel_child_id) AS (
			SELECT
				rel_parent_id,
				rel_child_id
			FROM hp_version_relationship
			WHERE rel_parent_id in ( select unnest($1) )
		UNION ALL
			SELECT
				c.rel_parent_id,
				c.rel_child_id
			FROM child_versions AS p, hp_version_relationship AS c
			WHERE c.rel_parent_id=p.rel_child_id
	)
	SELECT DISTINCT
		rel_parent_id,
		rel_child_id
	FROM child_versions;
$BODY$
  LANGUAGE sql VOLATILE
  COST 100
  ROWS 1000;




-- -----------------------------------------------------------------------------

-- DROP FUNCTION fcn_directory_recursive(integer);

CREATE OR REPLACE FUNCTION fcn_directory_recursive(IN v_dir_id integer, OUT parent_id integer, OUT dir_id integer)
  RETURNS SETOF record AS
$BODY$
		WITH RECURSIVE included_dirs (parent_id, dir_id) AS (
				SELECT
					parent_id,
					dir_id
				FROM hp_directory
				WHERE dir_id=$1
			UNION
				SELECT
					c.parent_id,
					c.dir_id
				FROM included_dirs AS p, hp_directory AS c
				WHERE c.parent_id=p.dir_id
		)
		SELECT
			parent_id,
			dir_id
		FROM included_dirs;
$BODY$
  LANGUAGE sql VOLATILE
  COST 100
  ROWS 1000;




-- -----------------------------------------------------------------------------

-- DROP FUNCTION fcn_reverse_path(integer);

CREATE OR REPLACE FUNCTION fcn_reverse_path(IN v_dir_id integer, OUT dir_id integer, OUT path text)
  RETURNS SETOF record AS
$BODY$
		with recursive rev_path (parent_id, dir_id, path) as (
				select
					parent_id,
					dir_id,
					dir_name::text as path
				from hp_directory
				where dir_id=1010
			union all
				select
					p.parent_id,
					p.dir_id,
					p.dir_name || '/' || path
				from rev_path as c, hp_directory as p
				where c.parent_id = p.dir_id
		)
		select
			1010::integer as dir_id,
			path
		from rev_path
		where parent_id is null;
$BODY$
  LANGUAGE sql VOLATILE
  COST 100
  ROWS 1000;




-- -----------------------------------------------------------------------------

-- DROP VIEW view_dir_tree;

CREATE OR REPLACE VIEW view_dir_tree AS 
 WITH RECURSIVE dir_tree(parent_id, dir_id, dir_name, rel_path) AS (
                 SELECT hp_directory.parent_id, hp_directory.dir_id, hp_directory.dir_name, ''::text AS rel_path
                   FROM hp_directory
                  WHERE hp_directory.parent_id IS NULL
        UNION ALL 
                 SELECT c.parent_id, c.dir_id, c.dir_name, (p.rel_path || '/'::text) || c.dir_name::text
                   FROM hp_directory c, dir_tree p
                  WHERE c.parent_id = p.dir_id
        )
 SELECT dir_tree.parent_id, dir_tree.dir_id, dir_tree.dir_name, dir_tree.rel_path
   FROM dir_tree;





-- -----------------------------------------------------------------------------

-- DROP FUNCTION fcn_latest_w_depends_by_dir(integer);

CREATE OR REPLACE FUNCTION fcn_latest_w_depends_by_dir(
	IN v_dir_id integer,
	OUT entry_id integer,
	OUT version_id integer,
	OUT dir_id integer,
	OUT entry_name varchar,
	OUT type_id integer,
	OUT file_ext varchar,
	OUT cat_id integer,
	OUT cat_name varchar,
	OUT file_size bigint,
	OUT str_latest_size varchar,
	OUT local_size bigint,
	OUT str_local_size varchar,
	OUT latest_stamp timestamp(6) without time zone,
	OUT str_latest_stamp varchar,
	OUT local_stamp timestamp(6) without time zone,
	OUT str_local_stamp varchar,
	OUT latest_md5 text,
	OUT local_md5 text,
	OUT checkout_user integer,
	OUT ck_user_name varchar,
	OUT checkout_date timestamp(6) without time zone,
	OUT str_checkout_date varchar,
	OUT checkout_node integer,
	OUT is_local boolean,
	OUT is_remote boolean,
	OUT client_status_code varchar,
	OUT relative_path varchar,
	OUT absolute_path varchar,
	OUT icon bytea,
	OUT is_depend_searched boolean,
	OUT is_readonly boolean
)
  RETURNS SETOF record AS
$BODY$
	-- several of the values are returned null because they are things only the client would know
	with dirs as (
		-- this and all child directories
		select dir_id from fcn_directory_recursive ($1)
	),
	lvs as (
		-- latest versions
		select distinct on (entry_id)
			entry_id,
			version_id,
			file_size,
			create_stamp,
			file_modify_stamp,
			md5sum
		from hp_version
		order by entry_id, create_stamp desc
	)
	select
		e.entry_id,
		v.version_id,
		e.dir_id,
		e.entry_name,
		t.type_id,
		t.file_ext,
		e.cat_id,
		c.cat_name,
		v.file_size::bigint as latest_size,
		pg_size_pretty(v.file_size) as str_latest_size,
		0::bigint as local_size,
		'0'::varchar as str_local_size,
		v.file_modify_stamp as latest_stamp,
		to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:mm:ss') as str_latest_stamp,
		null::timestamp as local_stamp,
		''::varchar as str_local_stamp,
		v.md5sum as latest_md5,
		null::text as local_md5,
		e.checkout_user,
		u.last_name || ', ' || u.first_name as ck_user_name,
		e.checkout_date,
		to_char(e.checkout_date, 'yyyy-MM-dd HH24:mm:ss') as str_checkout_date,
		e.checkout_node,
		false as is_local,
		true as is_remote,
		'ro'::varchar as client_status_code,
		'pwa' || replace(d.rel_path, '/', '\') as relative_path,
		null::varchar as absolute_path,
		t.icon,
		false as is_depend_searched,
		null::boolean as is_readonly
	from hp_entry as e
	left join hp_user as u on u.user_id=e.checkout_user
	left join hp_category as c on c.cat_id=e.cat_id
	left join hp_type as t on t.type_id=e.type_id
	left join view_dir_tree as d on d.dir_id = e.dir_id
	left join lvs as v on v.entry_id=e.entry_id
	where e.dir_id in (select dir_id from dirs)
	or e.entry_id in (
		-- get dependency entries
		select distinct v.entry_id
		from fcn_dependency_recursive(
			(select ARRAY(
				-- using latest versions for entries under the specified directory
				select v.version_id
				from lvs as v
				left join hp_entry as e on e.entry_id=v.entry_id
				where e.dir_id in (select dir_id from dirs)
			))
		) as r
		left join hp_version as v on v.version_id=r.rel_child_id
	)
	order by dir_id,entry_id;
$BODY$
  LANGUAGE sql VOLATILE
  COST 100
  ROWS 1000;




-- -----------------------------------------------------------------------------

-- DROP FUNCTION fcn_latest_w_depends_by_entry_list(integer[]);

CREATE OR REPLACE FUNCTION fcn_latest_w_depends_by_entry_list(
	IN v_version_ids integer[],
	OUT entry_id integer,
	OUT version_id integer,
	OUT dir_id integer,
	OUT entry_name varchar,
	OUT type_id integer,
	OUT file_ext varchar,
	OUT cat_id integer,
	OUT cat_name varchar,
	OUT file_size bigint,
	OUT str_latest_size varchar,
	OUT local_size bigint,
	OUT str_local_size varchar,
	OUT latest_stamp timestamp(6) without time zone,
	OUT str_latest_stamp varchar,
	OUT local_stamp timestamp(6) without time zone,
	OUT str_local_stamp varchar,
	OUT latest_md5 text,
	OUT local_md5 text,
	OUT checkout_user integer,
	OUT ck_user_name varchar,
	OUT checkout_date timestamp(6) without time zone,
	OUT str_checkout_date varchar,
	OUT checkout_node integer,
	OUT is_local boolean,
	OUT is_remote boolean,
	OUT client_status_code varchar,
	OUT relative_path varchar,
	OUT absolute_path varchar,
	OUT icon bytea,
	OUT is_depend_searched boolean,
	OUT is_readonly boolean
)
  RETURNS SETOF record AS
$BODY$
	-- several of the values are returned null because they are things only the client would know
	with lvs as (
		-- latest versions
		select distinct on (entry_id)
			entry_id,
			version_id,
			file_size,
			create_stamp,
			file_modify_stamp,
			md5sum
		from hp_version
		order by entry_id, create_stamp desc
	)
	select
		e.entry_id,
		v.version_id,
		e.dir_id,
		e.entry_name,
		t.type_id,
		t.file_ext,
		e.cat_id,
		c.cat_name,
		v.file_size::bigint as latest_size,
		pg_size_pretty(v.file_size) as str_latest_size,
		0::bigint as local_size,
		'0'::varchar as str_local_size,
		v.file_modify_stamp as latest_stamp,
		to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:mm:ss') as str_latest_stamp,
		null::timestamp as local_stamp,
		''::varchar as str_local_stamp,
		v.md5sum as latest_md5,
		null::text as local_md5,
		e.checkout_user,
		u.last_name || ', ' || u.first_name as ck_user_name,
		e.checkout_date,
		to_char(e.checkout_date, 'yyyy-MM-dd HH24:mm:ss') as str_checkout_date,
		e.checkout_node,
		false as is_local,
		true as is_remote,
		'ro'::varchar as client_status_code,
		'pwa' || replace(d.rel_path, '/', '\') as relative_path,
		null::varchar as absolute_path,
		t.icon,
		false as is_depend_searched,
		null::boolean as is_readonly
	from hp_entry as e
	left join hp_user as u on u.user_id=e.checkout_user
	left join hp_category as c on c.cat_id=e.cat_id
	left join hp_type as t on t.type_id=e.type_id
	left join view_dir_tree as d on d.dir_id = e.dir_id
	left join lvs as v on v.entry_id=e.entry_id
	where e.entry_id in ( select unnest($1) )
	or e.entry_id in (
		-- get dependency entries
		select distinct v.entry_id
		from fcn_dependency_recursive(
			(select ARRAY(
				-- using latest versions for entries under the specified directory
				select v.version_id
				from lvs as v
				left join hp_entry as e on e.entry_id=v.entry_id
				where e.entry_id in ( select unnest($1) )
			))
		) as r
		left join hp_version as v on v.version_id=r.rel_child_id
	)
	order by dir_id,entry_id;
$BODY$
  LANGUAGE sql VOLATILE
  COST 100
  ROWS 1000;




-- -----------------------------------------------------------------------------

-- DROP FUNCTION fcn_latest_w_depends_by_dir_list(integer[]);

CREATE OR REPLACE FUNCTION fcn_latest_w_depends_by_dir_list(
	IN v_dir_ids integer[],
	OUT entry_id integer,
	OUT version_id integer,
	OUT dir_id integer,
	OUT entry_name varchar,
	OUT type_id integer,
	OUT file_ext varchar,
	OUT cat_id integer,
	OUT cat_name varchar,
	OUT file_size bigint,
	OUT str_latest_size varchar,
	OUT local_size bigint,
	OUT str_local_size varchar,
	OUT latest_stamp timestamp(6) without time zone,
	OUT str_latest_stamp varchar,
	OUT local_stamp timestamp(6) without time zone,
	OUT str_local_stamp varchar,
	OUT latest_md5 text,
	OUT local_md5 text,
	OUT checkout_user integer,
	OUT ck_user_name varchar,
	OUT checkout_date timestamp(6) without time zone,
	OUT str_checkout_date varchar,
	OUT checkout_node integer,
	OUT is_local boolean,
	OUT is_remote boolean,
	OUT client_status_code varchar,
	OUT relative_path varchar,
	OUT absolute_path varchar,
	OUT icon bytea,
	OUT is_depend_searched boolean,
	OUT is_readonly boolean
)
  RETURNS SETOF record AS
$BODY$
	-- several of the values are returned null because they are things only the client would know
	with lvs as (
		-- latest versions
		select distinct on (entry_id)
			entry_id,
			version_id,
			file_size,
			create_stamp,
			file_modify_stamp,
			md5sum
		from hp_version
		order by entry_id, create_stamp desc
	)
	select
		e.entry_id,
		v.version_id,
		e.dir_id,
		e.entry_name,
		t.type_id,
		t.file_ext,
		e.cat_id,
		c.cat_name,
		v.file_size::bigint as latest_size,
		pg_size_pretty(v.file_size) as str_latest_size,
		0::bigint as local_size,
		'0'::varchar as str_local_size,
		v.file_modify_stamp as latest_stamp,
		to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:mm:ss') as str_latest_stamp,
		null::timestamp as local_stamp,
		''::varchar as str_local_stamp,
		v.md5sum as latest_md5,
		null::text as local_md5,
		e.checkout_user,
		u.last_name || ', ' || u.first_name as ck_user_name,
		e.checkout_date,
		to_char(e.checkout_date, 'yyyy-MM-dd HH24:mm:ss') as str_checkout_date,
		e.checkout_node,
		false as is_local,
		true as is_remote,
		'ro'::varchar as client_status_code,
		'pwa' || replace(d.rel_path, '/', '\') as relative_path,
		null::varchar as absolute_path,
		t.icon,
		false as is_depend_searched,
		null::boolean as is_readonly
	from hp_entry as e
	left join hp_user as u on u.user_id=e.checkout_user
	left join hp_category as c on c.cat_id=e.cat_id
	left join hp_type as t on t.type_id=e.type_id
	left join view_dir_tree as d on d.dir_id = e.dir_id
	left join lvs as v on v.entry_id=e.entry_id
	where e.dir_id in (select unnest($1))
	or e.entry_id in (
		-- get dependency entries
		select distinct v.entry_id
		from fcn_dependency_recursive(
			(select ARRAY(
				-- using latest versions for entries under the specified directory
				select v.version_id
				from lvs as v
				left join hp_entry as e on e.entry_id=v.entry_id
				where e.dir_id in (select unnest($1))
			))
		) as r
		left join hp_version as v on v.version_id=r.rel_child_id
	)
	order by dir_id,entry_id;
$BODY$
  LANGUAGE sql VOLATILE
  COST 100
  ROWS 1000;




-- -----------------------------------------------------------------------------
-- set owner on all sequences
alter sequence seq_hp_category_cat_id      owner to engadmin;
alter sequence seq_hp_directory_dir_id     owner to engadmin;
alter sequence seq_hp_entry_entry_id       owner to engadmin;
alter sequence seq_hp_node_node_id         owner to engadmin;
alter sequence seq_hp_property_prop_id     owner to engadmin;
alter sequence seq_hp_type_type_id         owner to engadmin;
alter sequence seq_hp_user_user_id         owner to engadmin;
alter sequence seq_hp_version_version_id   owner to engadmin;
alter sequence seq_hp_entry_name_filter_id owner to engadmin;





