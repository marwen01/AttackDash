let map = null;
let markersLayer = null;

function initAttackMap(elementId, markers) {
    console.log("initAttackMap called", elementId, markers);

    // Initialize the map centered on the world
    map = L.map(elementId, {
        center: [30, 0],
        zoom: 2,
        minZoom: 2,
        maxZoom: 8,
        zoomControl: false,
        attributionControl: false
    });

    // Dark map tiles (CartoDB Dark Matter)
    L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
        maxZoom: 19
    }).addTo(map);

    // Create markers layer
    markersLayer = L.layerGroup().addTo(map);

    // Add initial markers
    updateMarkers(markers);
}

function updateMarkers(markers) {
    console.log("updateMarkers called", markers);
    if (!map || !markersLayer) {
        console.log("Map or markersLayer not ready", map, markersLayer);
        return;
    }

    // Handle case where markers is a single object or an array
    if (!markers) {
        console.log("No markers");
        return;
    }

    // Convert single object to array
    if (!Array.isArray(markers)) {
        markers = [markers];
    }

    console.log("Adding", markers.length, "markers");

    // Clear existing markers
    markersLayer.clearLayers();

    // Add new markers
    markers.forEach(marker => {
        if (marker.lat && marker.lng) {
            // Calculate marker size based on count
            const size = Math.min(Math.max(marker.count / 10, 10), 50);

            // Create a pulsing circle marker
            const circleMarker = L.circleMarker([marker.lat, marker.lng], {
                radius: size,
                fillColor: '#008f6b',
                fillOpacity: 0.6,
                color: '#00a878',
                weight: 2,
                opacity: 0.8
            });

            // Add popup with info
            circleMarker.bindPopup(`
                <div style="text-align: center; color: #e0e0e0;">
                    <strong>${marker.country}</strong><br>
                    <span style="font-size: 1.2em; color: #008f6b;">${marker.count}</span> attacks
                </div>
            `);

            // Add tooltip
            circleMarker.bindTooltip(`${marker.country}: ${marker.count}`, {
                permanent: false,
                direction: 'top',
                className: 'attack-tooltip'
            });

            markersLayer.addLayer(circleMarker);

            // Add animated pulse effect for high-count countries
            if (marker.count > 50) {
                addPulseEffect(marker.lat, marker.lng, size);
            }
        }
    });
}

function addPulseEffect(lat, lng, size) {
    const pulseIcon = L.divIcon({
        className: 'pulse-marker',
        html: `<div class="pulse-ring" style="width: ${size * 3}px; height: ${size * 3}px;"></div>`,
        iconSize: [size * 3, size * 3],
        iconAnchor: [size * 1.5, size * 1.5]
    });

    const pulseMarker = L.marker([lat, lng], { icon: pulseIcon, interactive: false });
    markersLayer.addLayer(pulseMarker);
}

// Add animated attack line from origin to destination
function addAttackLine(fromLat, fromLng, toLat, toLng) {
    if (!map) return;

    const line = L.polyline([[fromLat, fromLng], [toLat, toLng]], {
        color: '#008f6b',
        weight: 2,
        opacity: 0.6,
        dashArray: '10, 10'
    }).addTo(map);

    // Animate and remove
    setTimeout(() => {
        map.removeLayer(line);
    }, 3000);
}

// Add CSS for pulse animation
const style = document.createElement('style');
style.textContent = `
    .pulse-ring {
        border: 3px solid #008f6b;
        border-radius: 50%;
        animation: pulseRing 2s infinite;
        opacity: 0;
    }

    @keyframes pulseRing {
        0% {
            transform: scale(0.5);
            opacity: 0.8;
        }
        100% {
            transform: scale(1.5);
            opacity: 0;
        }
    }

    .attack-tooltip {
        background: rgba(10, 10, 15, 0.9) !important;
        border: 1px solid #008f6b !important;
        color: #fff !important;
        border-radius: 6px !important;
        padding: 6px 10px !important;
    }

    .attack-tooltip::before {
        border-top-color: #008f6b !important;
    }

    .leaflet-popup-content-wrapper {
        background: rgba(10, 10, 15, 0.95);
        border: 1px solid #008f6b;
        border-radius: 8px;
    }

    .leaflet-popup-tip {
        background: rgba(10, 10, 15, 0.95);
        border: 1px solid #008f6b;
    }

    .leaflet-popup-content {
        color: #fff;
        margin: 10px 15px;
    }
`;
document.head.appendChild(style);
