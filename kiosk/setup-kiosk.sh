#!/bin/bash
# Raspberry Pi Zero 2 W Kiosk Setup for AttackDash
# Run as: sudo bash setup-kiosk.sh

DASHBOARD_URL="http://docker.lexbit.se:8127/"
KIOSK_USER="pi"

echo "=== AttackDash Kiosk Setup ==="

# Update system
echo "Updating system..."
apt-get update && apt-get upgrade -y

# Install minimal X environment and Chromium
echo "Installing required packages..."
apt-get install -y --no-install-recommends \
    xserver-xorg \
    x11-xserver-utils \
    xinit \
    openbox \
    chromium-browser \
    unclutter \
    sed

# Create kiosk startup script
echo "Creating kiosk startup script..."
cat > /home/$KIOSK_USER/kiosk.sh << 'KIOSKEOF'
#!/bin/bash

DASHBOARD_URL="http://docker.lexbit.se:8127/"
MAX_RETRIES=30
RETRY_DELAY=5

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

# Start Chromium in kiosk mode with auto-restart on crash
while true; do
    chromium-browser \
        --kiosk \
        --noerrdialogs \
        --disable-infobars \
        --disable-session-crashed-bubble \
        --disable-restore-session-state \
        --disable-features=TranslateUI \
        --disable-pinch \
        --overscroll-history-navigation=0 \
        --check-for-update-interval=31536000 \
        --disable-component-update \
        --autoplay-policy=no-user-gesture-required \
        --incognito \
        "$DASHBOARD_URL"

    echo "Chromium crashed or closed, restarting in 5 seconds..."
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
if ! grep -q "consoleblank=0" /boot/cmdline.txt; then
    sed -i 's/$/ consoleblank=0/' /boot/cmdline.txt
fi

# Optional: Set GPU memory for smoother graphics
echo "Configuring GPU memory..."
if ! grep -q "gpu_mem" /boot/config.txt; then
    echo "gpu_mem=128" >> /boot/config.txt
fi

# Optional: Disable underscan/overscan
if ! grep -q "disable_overscan" /boot/config.txt; then
    echo "disable_overscan=1" >> /boot/config.txt
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
