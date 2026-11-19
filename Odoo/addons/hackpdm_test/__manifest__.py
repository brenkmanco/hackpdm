{
    'name': 'hackpdm_test',
    'version': '16.0.1.0.0',
    'category': 'Engineering',
    'summary': 'Test models and fixture records for HackPDM development and CI',
    'description': 'Isolated hp.test model, ACL, and seed fixtures excluded from production hackpdm installs.',
    'author': 'Justin Johnson',
    'license': 'LGPL-3',
    'depends': [
        'hackpdm',
    ],
    'data': [
        'security/ir.model.access.csv',
        'data/hp_test_data.xml',
    ],
    'installable': True,
    'application': False,
}
