{
    'name': 'hackpdm',
    'version': '16.0.1.2.0',
    'category': 'Engineering',
    'summary': 'product data management',
    'description': 'engineering product data management system',
    'author': 'Justin Johnson',
    'depends': [
        'base'
    ],
    'data': [
        'security/hackpdm_groups.xml',
        'security/ir.model.access.csv',
        'security/hackpdm_rules.xml',
        'views/hp_directory_views.xml',
        'views/hp_entry_views.xml',
        'views/hp_version_views.xml',
        'data/hp_settings_data.xml',
        'data/hp_upload_cron.xml',
    ],
    'installable': True,
    'application': True,
}
