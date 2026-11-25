// Leaflet is loaded globally (L) from App.razor.
const maps = new WeakMap();

export function init(el) {
    // Default view: Cebu City. setPins re-fits the view to the courts.
    const map = L.map(el, { scrollWheelZoom: false }).setView([10.3157, 123.8854], 11);

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    // Don't hijack page scrolling until the user clicks the map
    map.on('click', () => map.scrollWheelZoom.enable());
    map.on('mouseout', () => map.scrollWheelZoom.disable());

    maps.set(el, { map, layer: L.layerGroup().addTo(map) });

    // The container may have just been laid out
    setTimeout(() => map.invalidateSize(), 0);
}

export function setPins(el, pins) {
    const entry = maps.get(el);
    if (!entry) return;

    const { map, layer } = entry;
    layer.clearLayers();

    const points = [];

    for (const p of pins) {
        const pin = document.createElement('span');
        pin.className = 'pin';
        pin.textContent = p.emoji;

        const icon = L.divIcon({
            className: 'pin-wrap',
            html: pin,
            iconSize: [36, 36],
            iconAnchor: [18, 18],
            popupAnchor: [0, -18]
        });

        // Popup built with textContent so court names can never inject HTML
        const box = document.createElement('div');
        box.className = 'pin-popup';

        const title = document.createElement('strong');
        title.textContent = p.name;

        const address = document.createElement('span');
        address.className = 'meta';
        address.textContent = p.address;

        const summary = document.createElement('span');
        summary.className = 'meta';
        summary.textContent = p.summary;

        const link = document.createElement('a');
        link.href = `courts/${p.id}`;
        link.textContent = 'View court →';

        box.append(title, address, summary, link);

        L.marker([p.latitude, p.longitude], { icon, title: p.name })
            .bindPopup(box)
            .addTo(layer);

        points.push([p.latitude, p.longitude]);
    }

    if (points.length > 0) {
        map.fitBounds(points, { padding: [40, 40], maxZoom: 15 });
    }
}

export function destroy(el) {
    const entry = maps.get(el);
    if (entry) {
        entry.map.remove();
        maps.delete(el);
    }
}