// Leaflet (L) is loaded globally from App.razor.
const pickers = new WeakMap();
const DEFAULT_CENTER = [10.3157, 123.8854]; // Cebu City

const round = n => Math.round(n * 1e6) / 1e6;

function notify(entry, latlng) {
    entry.dotNet
        .invokeMethodAsync('OnMapPicked', round(latlng.lat), round(latlng.lng))
        .catch(() => { /* circuit is gone */ });
}

function placeMarker(entry, latlng) {
    if (entry.marker) {
        entry.marker.setLatLng(latlng);
        return;
    }
    entry.marker = L.marker(latlng, { draggable: true, autoPan: true }).addTo(entry.map);
    entry.marker.on('dragend', () => notify(entry, entry.marker.getLatLng()));
}

export function init(el, dotNet, lat, lng) {
    const hasPoint = lat != null && lng != null;
    const map = L.map(el, { scrollWheelZoom: false })
        .setView(hasPoint ? [lat, lng] : DEFAULT_CENTER, hasPoint ? 16 : 12);

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    const entry = { map, marker: null, dotNet };
    pickers.set(el, entry);

    if (hasPoint) placeMarker(entry, [lat, lng]);

    map.on('click', e => {
        placeMarker(entry, e.latlng);
        notify(entry, e.latlng);
        map.scrollWheelZoom.enable();
    });
    map.on('mouseout', () => map.scrollWheelZoom.disable());

    setTimeout(() => map.invalidateSize(), 0);
}

// Called when the latitude/longitude inputs change
export function setPoint(el, lat, lng) {
    const entry = pickers.get(el);
    if (!entry) return;
    placeMarker(entry, [lat, lng]);
    entry.map.setView([lat, lng], Math.max(entry.map.getZoom(), 16));
}

// Resolves to null on success, or 'denied' | 'unsupported' | 'unavailable'
export function locate(el) {
    return new Promise(resolve => {
        if (!navigator.geolocation) return resolve('unsupported');

        navigator.geolocation.getCurrentPosition(
            pos => {
                const entry = pickers.get(el);
                if (!entry) return resolve(null);
                const latlng = L.latLng(pos.coords.latitude, pos.coords.longitude);
                placeMarker(entry, latlng);
                entry.map.setView(latlng, 17);
                notify(entry, latlng);
                resolve(null);
            },
            err => resolve(err.code === 1 ? 'denied' : 'unavailable'),
            { enableHighAccuracy: true, timeout: 10000 }
        );
    });
}

export function destroy(el) {
    const entry = pickers.get(el);
    if (entry) {
        entry.map.remove();
        pickers.delete(el);
    }
}