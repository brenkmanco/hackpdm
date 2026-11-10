# Odoo 16.0 image with the extra Python packages the hackpdm addon imports.
# Used by .github/workflows/odoo-integration.yml
FROM odoo:16.0

USER root
RUN pip3 install --no-cache-dir \
        "numpy<2.1" \
        "sqlalchemy>=2,<3" \
        webdavclient3 \
        python-magic
USER odoo
