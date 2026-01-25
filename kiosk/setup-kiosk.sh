#!/bin/bash
# Raspberry Pi Kiosk Setup for AttackDash
# Tested on Pi 3B+ and Pi Zero 2 W
# Run as: sudo bash setup-kiosk.sh

DASHBOARD_URL="http://docker.lexbit.se:8127/"
KIOSK_USER="${SUDO_USER:-pi}"

echo "=== AttackDash Kiosk Setup ==="
echo "Kiosk user: $KIOSK_USER"

# Detect config.txt location (newer Pi OS uses /boot/firmware/)
if [ -f /boot/firmware/config.txt ]; then
    BOOT_CONFIG="/boot/firmware/config.txt"
    CMDLINE="/boot/firmware/cmdline.txt"
else
    BOOT_CONFIG="/boot/config.txt"
    CMDLINE="/boot/cmdline.txt"
fi
echo "Boot config: $BOOT_CONFIG"

# Update system
echo "Updating system..."
apt-get update && apt-get upgrade -y

# Install minimal X environment and Firefox
echo "Installing required packages..."
apt-get install -y --no-install-recommends \
    xserver-xorg \
    x11-xserver-utils \
    xinit \
    openbox \
    firefox-esr \
    unclutter \
    xdotool \
    curl

# Create kiosk startup script
echo "Creating kiosk startup script..."
cat > /home/$KIOSK_USER/kiosk.sh << 'KIOSKEOF'
#!/bin/bash

DASHBOARD_URL="http://docker.lexbit.se:8127/"
MAX_RETRIES=30
RETRY_DELAY=5
export DISPLAY=:0

# Wait for network
wait_for_network() {
    echo "Waiting for network..."
    for i in $(seq 1 $MAX_RETRIES); do
        if ping -c 1 -W 2 docker.lexbit.se > /dev/null 2>&1; then
            echo "Network available!"
            return 0
        fi
        echo "Retry $i/$MAX_RETRIES..."
        sleep $RETRY_DELAY
    done
    echo "Network not available after $MAX_RETRIES attempts"
    return 1
}

# Wait for dashboard to be reachable
wait_for_dashboard() {
    echo "Waiting for dashboard..."
    for i in $(seq 1 $MAX_RETRIES); do
        if curl -s --head --max-time 5 "$DASHBOARD_URL" > /dev/null 2>&1; then
            echo "Dashboard reachable!"
            return 0
        fi
        echo "Dashboard not ready, retry $i/$MAX_RETRIES..."
        sleep $RETRY_DELAY
    done
    echo "Dashboard not reachable after $MAX_RETRIES attempts"
    return 1
}

# Disable screen blanking
xset s off
xset s noblank
xset -dpms

# Hide mouse cursor after 0.5 seconds of inactivity
unclutter -idle 0.5 -root &

# Wait for connectivity
wait_for_network
wait_for_dashboard

# Start Firefox in kiosk mode with auto-restart on crash
while true; do
    firefox-esr --kiosk "$DASHBOARD_URL"

    echo "Firefox crashed or closed, restarting in 5 seconds..."
    sleep 5
done
KIOSKEOF

chmod +x /home/$KIOSK_USER/kiosk.sh
chown $KIOSK_USER:$KIOSK_USER /home/$KIOSK_USER/kiosk.sh

# Create openbox autostart
echo "Configuring openbox..."
mkdir -p /home/$KIOSK_USER/.config/openbox
cat > /home/$KIOSK_USER/.config/openbox/autostart << EOF
/home/$KIOSK_USER/kiosk.sh &
EOF
chown -R $KIOSK_USER:$KIOSK_USER /home/$KIOSK_USER/.config

# Create .xinitrc for startx
echo "Creating .xinitrc..."
cat > /home/$KIOSK_USER/.xinitrc << EOF
exec openbox-session
EOF
chown $KIOSK_USER:$KIOSK_USER /home/$KIOSK_USER/.xinitrc

# Auto-login and start X on boot
echo "Configuring auto-login..."
mkdir -p /etc/systemd/system/getty@tty1.service.d
cat > /etc/systemd/system/getty@tty1.service.d/autologin.conf << EOF
[Service]
ExecStart=
ExecStart=-/sbin/agetty --autologin $KIOSK_USER --noclear %I \$TERM
EOF

# Start X automatically on login
echo "Configuring X autostart..."
cat >> /home/$KIOSK_USER/.bash_profile << 'EOF'

# Start X on login (only on tty1)
if [ -z "$DISPLAY" ] && [ "$(tty)" = "/dev/tty1" ]; then
    startx -- -nocursor
fi
EOF
chown $KIOSK_USER:$KIOSK_USER /home/$KIOSK_USER/.bash_profile

# Disable screen blanking in boot config
echo "Disabling screen blanking..."
if [ -f "$CMDLINE" ] && ! grep -q "consoleblank=0" "$CMDLINE"; then
    sed -i 's/$/ consoleblank=0/' "$CMDLINE"
fi

# Configure display driver - use fkms for better HDMI compatibility
echo "Configuring display driver..."
if grep -q "dtoverlay=vc4-kms-v3d" "$BOOT_CONFIG"; then
    sed -i 's/dtoverlay=vc4-kms-v3d/dtoverlay=vc4-fkms-v3d/' "$BOOT_CONFIG"
    echo "Changed vc4-kms-v3d to vc4-fkms-v3d for better HDMI compatibility"
fi

# Force HDMI output
echo "Configuring HDMI..."
if ! grep -q "hdmi_force_hotplug" "$BOOT_CONFIG"; then
    echo "hdmi_force_hotplug=1" >> "$BOOT_CONFIG"
fi

if ! grep -q "hdmi_group" "$BOOT_CONFIG"; then
    echo "hdmi_group=1" >> "$BOOT_CONFIG"
fi

if ! grep -q "hdmi_mode" "$BOOT_CONFIG"; then
    echo "hdmi_mode=16" >> "$BOOT_CONFIG"
fi

# Set GPU memory for smoother graphics
echo "Configuring GPU memory..."
if ! grep -q "gpu_mem" "$BOOT_CONFIG"; then
    echo "gpu_mem=128" >> "$BOOT_CONFIG"
fi

# Disable underscan/overscan
if ! grep -q "disable_overscan" "$BOOT_CONFIG"; then
    echo "disable_overscan=1" >> "$BOOT_CONFIG"
fi

echo ""
echo "=== Setup Complete ==="
echo "Dashboard URL: $DASHBOARD_URL"
echo ""
echo "Reboot to start kiosk mode:"
echo "  sudo reboot"
echo ""
echo "To change the dashboard URL, edit:"
echo "  /home/$KIOSK_USER/kiosk.sh"
