(() => {
    const instances = new Map();

    function toSet(items) {
        return new Set((items || []).map(c => (c || '').toUpperCase()));
    }

    function styleFor(feature, visitedSet, plannedSet) {
        const code = (feature.id || '').toUpperCase();
        const isVisited = visitedSet.has(code);
        const isPlanned = plannedSet.has(code);

        if (isVisited) {
            return {
                color: '#8ef7b5',
                weight: 1,
                fillColor: '#2dd28a',
                fillOpacity: 0.7
            };
        }

        if (isPlanned) {
            return {
                color: '#ffb3c9',
                weight: 1,
                fillColor: '#ff7b9c',
                fillOpacity: 0.5
            };
        }

        return {
            color: '#555',
            weight: 1,
            fillColor: '#1c1a2a',
            fillOpacity: 0.2
        };
    }

    function bindTooltip(feature, layer, visitedSet, plannedSet) {
        const code = (feature.id || '').toUpperCase();
        const status = visitedSet.has(code)
            ? 'Bereits besucht'
            : plannedSet.has(code)
                ? 'Geplant'
                : 'Noch offen';

        const name = feature.properties?.name || code;
        layer.bindTooltip(`${name} – ${status}`, { sticky: true });
    }

    async function ensureInstance(elementId, geoJsonUrl, visitedCodes, plannedCodes) {
        if (typeof L === "undefined") {
            setTimeout(() => ensureInstance(elementId, geoJsonUrl, visitedCodes, plannedCodes), 200);
            return;
        }

        const visitedSet = toSet(visitedCodes);
        const plannedSet = toSet(plannedCodes);

        let instance = instances.get(elementId);
        if (!instance) {
            const map = L.map(elementId, {
                worldCopyJump: true,
                scrollWheelZoom: true,
                zoomControl: true
            });

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 5,
                minZoom: 2,
                attribution: '&copy; OpenStreetMap'
            }).addTo(map);

            const response = await fetch(geoJsonUrl);
            const geoJson = await response.json();

            const layer = L.geoJSON(geoJson, {
                style: feature => styleFor(feature, visitedSet, plannedSet),
                onEachFeature: (feature, layer) => bindTooltip(feature, layer, visitedSet, plannedSet)
            });

            layer.addTo(map);
            map.fitBounds(layer.getBounds(), { padding: [20, 20] });

            instance = { map, layer };
            instances.set(elementId, instance);
            return;
        }

        instance.layer.setStyle(feature => styleFor(feature, visitedSet, plannedSet));
    }

    window.travelMap = {
        update: ensureInstance
    };
})();
