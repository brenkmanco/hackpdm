{
    'name': 'hackpdm',
    'version': '16.0.1.0.0',
    'category': 'Engineering',
    'summary': 'product data management',
    'description': 'engineering product data management system',
    'author': 'Justin Johnson',
    'depends': ['base', 'stock'],
    'data': [
        'views/custom_view.xml',
        'security/ir.model.access.csv',
        'data/hp_settings_data.xml',
    ],
    'installable': True,
    'application': True,
}
