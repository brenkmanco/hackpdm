#!/bin/bash

# Variables
OdooVenvName="odoo-venv"
OdooDir="$(pwd)"
PsqlName="odoo"
PsqlPass="odoo"
FileStoreDir="/mnt/datastore"

# Prepare environment
sudo apt update
sudo apt upgrade -y
sudo apt install -y git python3 python3-pip build-essential wget
sudo apt install -y python3-dev libpq-dev libxml2-dev libxslt1-dev libldap2-dev libsasl2-dev libffi-dev
sudo apt install -y wkhtmltopdf

# Install PostgreSQL
sudo apt install -y postgresql
sudo systemctl enable postgresql
sudo systemctl start postgresql

# Create PostgreSQL user
sudo su - postgres -c "createuser -s $PsqlName"

# Install Odoo source
sudo mkdir -p /opt
cd /opt
sudo git clone --depth 1 --branch 16.0 https://github.com/odoo/odoo.git
cd /opt/odoo

# Create Python venv
echo "creating venv in $OdooDir/$OdooVenvName"
python3 -m venv $OdooDir/$OdooVenvName
source "$OdooDir/$OdooVenvName/bin/activate"

# Install Python dependencies
pip install wheel SQLAlchemy numpy python-magic webdavclient3 setuptools
pip install -r requirements.txt
pip install --upgrade pip setuptools wheel

# Configure Odoo environment
mkdir -p "$FileStoreDir"
mkdir -p "$OdooDir/config"

# Copy config template
cp /opt/odoo/debian/odoo.conf "$OdooDir/config/odoo.conf"

# Symlink odoo-bin

# Write odoo.conf
cat <<EOF > "$OdooDir/config/odoo.conf"
[options]
db_host = localhost
db_port = 5432
db_user = $PsqlName
db_password = $PsqlPass
data_dir = $FileStoreDir
addons_path = /opt/odoo/addons,$OdooDir/addons
without_demo = True
default_productivity_apps = True
logfile = /var/log/odoo/odoo.log
EOF

# Create log directory
sudo mkdir -p /var/log/odoo
sudo chown -R $USER:$USER /var/log/odoo

# Create systemd service
cat <<EOF | sudo tee /etc/systemd/system/odoo.service
[Unit]
Description=Odoo
After=network.target postgresql.service

[Service]
Type=simple
User=$USER
ExecStart=$OdooDir/$OdooVenvName/bin/python3 /opt/odoo/odoo-bin -c $OdooDir/config/odoo.conf
Restart=always

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl disable odoo
