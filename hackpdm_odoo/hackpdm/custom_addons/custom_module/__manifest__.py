{
    'name': 'Custom Module',
    'version': '1.0',
    'category': 'Custom',
    'summary': 'A custom module',
    'description': 'A test for a custom module',
    'author': 'Justin Johnson',
    'depends': ['base', 'stock'],
    'data': [
        'views/custom_view.xml',
        'security/ir.model.access.csv',
    ],
    'installable': True,
    'application': True,
}