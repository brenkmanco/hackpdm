#!/bin/bash

# Ensure DISPLAY is set to the active graphical session
export DISPLAY=:0

# Kill any running Odoo server processes
pkill -f "python.*odoo-bin"

# Optional: set XAUTHORITY if needed (depends on your setup)
# export XAUTHORITY=/home/moony/.Xauthority

# Launch a visible terminal window on the remote desktop
gnome-terminal -- bash -c "
    source odoo-venv/bin/activate &&
    echo 'Launching Odoo server...' &&
    /opt/odoo/odoo-bin odoo-venv/bin/activate -c config/odoo.conf -d odoopdm -u hackpdm;
    exec bash"
