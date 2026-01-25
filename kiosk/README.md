# AttackDash Kiosk Setup for Raspberry Pi

Tested on:
- Raspberry Pi 3B+ (recommended - 1GB RAM)
- Raspberry Pi Zero 2 W (512MB RAM - works but slower)

## Prerequisites

- Raspberry Pi 3B+ or Zero 2 W
- microSD card (8GB+)
- Raspberry Pi OS Lite (32-bit) - No desktop environment needed
- HDMI display/TV

## Installation

### 1. Flash Raspberry Pi OS Lite

Use Raspberry Pi Imager to flash "Raspberry Pi OS Lite (32-bit)" to your SD card.

In the imager settings (gear icon), configure:
- Hostname: `attackdash-kiosk`
- Enable SSH
- Set username/password
- Configure WiFi

### 2. Boot and Connect

Insert SD card, boot the Pi, and SSH in:
```bash
ssh pi@attackdash-kiosk.local
```

### 3. Run Setup Script

```bash
# Download and run the setup script
curl -O https://raw.githubusercontent.com/marwen01/AttackDash/master/kiosk/setup-kiosk.sh
sudo bash setup-kiosk.sh

# Reboot to start kiosk
sudo reboot
```

## What the Setup Does

- Installs minimal X server, Openbox, and Firefox ESR
- Configures auto-login on boot
- Starts Firefox in kiosk mode pointing to the dashboard
- Waits for network/dashboard with retries before starting browser
- Auto-restarts browser if it crashes
- Hides mouse cursor
- Disables screen blanking and power saving
- Configures HDMI for optimal TV compatibility (fkms driver)

## Configuration

### Change Dashboard URL

Edit `/home/<user>/kiosk.sh` and change the `DASHBOARD_URL` variable.

### Adjust Retry Settings

In `/home/<user>/kiosk.sh`:
- `MAX_RETRIES`: Number of connection attempts (default: 30)
- `RETRY_DELAY`: Seconds between retries (default: 5)

### Rotate Display

Add to `/boot/firmware/config.txt` (or `/boot/config.txt` on older Pi OS):
```
# Rotate 90 degrees
display_rotate=1

# Rotate 180 degrees
display_rotate=2

# Rotate 270 degrees
display_rotate=3
```

### Exit Kiosk Mode (for maintenance)

Press `Alt+F4` to close Firefox, then `Ctrl+Alt+F2` for a terminal.

Or SSH in from another machine.

## Troubleshooting

### Black screen / No display

The setup script configures HDMI automatically. If you still have issues:

1. Check HDMI cable connection
2. Verify these settings in `/boot/firmware/config.txt`:
```
dtoverlay=vc4-fkms-v3d
hdmi_force_hotplug=1
hdmi_group=1
hdmi_mode=16
```

### Dashboard not loading

- Check network: `ping docker.lexbit.se`
- Check dashboard: `curl -I http://docker.lexbit.se:8127/`
- Check logs: `journalctl -u getty@tty1`

### Browser crashes repeatedly

- Check memory: `free -h`
- Pi Zero 2 W has limited RAM (512MB) - some dashboard features may be slow
- Pi 3B+ (1GB RAM) is recommended for smoother experience

### Taking a screenshot (for debugging)

```bash
sudo apt install scrot
DISPLAY=:0 scrot /tmp/screenshot.png
```
