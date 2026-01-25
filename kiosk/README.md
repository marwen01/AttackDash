# AttackDash Kiosk Setup for Raspberry Pi Zero 2 W

## Prerequisites

- Raspberry Pi Zero 2 W
- microSD card (8GB+)
- Raspberry Pi OS Lite (32-bit) - No desktop environment needed

## Installation

### 1. Flash Raspberry Pi OS Lite

Use Raspberry Pi Imager to flash "Raspberry Pi OS Lite (32-bit)" to your SD card.

In the imager settings (gear icon), configure:
- Hostname: `attackdash-kiosk`
- Enable SSH
- Set username/password (default: pi)
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

- Installs minimal X server, Openbox, and Chromium
- Configures auto-login on boot
- Starts Chromium in kiosk mode pointing to the dashboard
- Waits for network/dashboard with retries before starting browser
- Auto-restarts browser if it crashes
- Hides mouse cursor
- Disables screen blanking and power saving

## Configuration

### Change Dashboard URL

Edit `/home/pi/kiosk.sh` and change the `DASHBOARD_URL` variable.

### Adjust Retry Settings

In `/home/pi/kiosk.sh`:
- `MAX_RETRIES`: Number of connection attempts (default: 30)
- `RETRY_DELAY`: Seconds between retries (default: 5)

### Rotate Display

Add to `/boot/config.txt`:
```
# Rotate 90 degrees
display_rotate=1

# Rotate 180 degrees
display_rotate=2

# Rotate 270 degrees
display_rotate=3
```

### Exit Kiosk Mode (for maintenance)

Press `Alt+F4` to close Chromium, then `Ctrl+Alt+F2` for a terminal.

Or SSH in from another machine.

## Troubleshooting

### Black screen / No display
- Check HDMI cable connection
- Try adding `hdmi_force_hotplug=1` to `/boot/config.txt`

### Dashboard not loading
- Check network: `ping docker.lexbit.se`
- Check dashboard: `curl -I http://docker.lexbit.se:8127/`
- Check logs: `journalctl -u getty@tty1`

### Browser crashes repeatedly
- Check memory: `free -h`
- Reduce GPU memory in `/boot/config.txt` if needed
