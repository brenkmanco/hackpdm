{
    'name': 'hackpdm',
    'version': '16.0.1.0.0',
    'category': 'Engineering',
    'summary': 'product data management',
    'description': 'engineering product data management system',
    'author': 'Justin Johnson',
    'depends': ['base'],
    'data': [
        'security/ir.model.access.csv',
        'views/hp_directory_views.xml',
        'data/hp_settings_data.xml',
    ],
    'installable': True,
    'application': True,
}
