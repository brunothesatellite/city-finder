(() => {
    "use strict";

    const mapElement = document.getElementById("map");
    const offlineElement = document.getElementById("map-offline");
    const emptyElement = document.getElementById("map-empty");
    const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;

    const BASEMAPS = [
        {
            id: "plan",
            name: "Plan",
            detail: "OpenFreeMap · clair et sombre",
            previewLight: "assets/preview-plan-light.jpg",
            previewDark: "assets/preview-plan-dark.jpg",
        },
        {
            id: "osm",
            name: "OpenStreetMap",
            detail: "Standard · mondial",
            preview: "assets/preview-osm.jpg",
        },
        {
            id: "satellite",
            name: "Satellite",
            detail: "Esri · imagerie aérienne",
            preview: "assets/preview-satellite.jpg",
        },
        {
            id: "relief",
            name: "Relief",
            detail: "OpenTopoMap · randonnée",
            preview: "assets/preview-relief.jpg",
        },
    ];

    const ATTRIBUTIONS = {
        plan:
            '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors · ' +
            '<a href="https://openfreemap.org/" target="_blank" rel="noopener noreferrer">OpenFreeMap</a> · ' +
            '<a href="https://www.openmaptiles.org/" target="_blank" rel="noopener noreferrer">OpenMapTiles</a>',
        osm:
            '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors',
        satellite:
            'Imagerie &copy; <a href="https://www.esri.com/" target="_blank" rel="noopener noreferrer">Esri</a>, Maxar, Earthstar Geographics · Libellés &copy; Esri',
        relief:
            'Fond &copy; <a href="https://opentopomap.org/" target="_blank" rel="noopener noreferrer">OpenTopoMap</a> (CC-BY-SA) · ' +
            '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors',
    };

    const state = {
        map: null,
        cluster: null,
        basemap: null,
        basemapToken: 0,
        layersContainer: null,
        attribution: "",
        markers: new Map(),
        points: [],
        pointsById: new Map(),
        activeId: null,
        theme: document.documentElement.dataset.theme === "dark" ? "dark" : "light",
        mode: "osm",
        hasFitted: false,
        autoFit: true,
        fitting: false,
        offline: false,
        thumbnails: new Map(),
        requestedThumbs: new Set(),
    };

    function post(message) {
        try {
            window.chrome?.webview?.postMessage(message);
        } catch {
            // The page can also be opened standalone for debugging.
        }
    }


    window.addEventListener("error", (event) => {
        post({ type: "jsError", message: event.message, line: event.lineno, source: event.filename });
    });
    window.addEventListener("unhandledrejection", (event) => {
        post({ type: "jsError", message: String(event.reason) });
    });

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function normalizeMode(mode) {
        return BASEMAPS.some((basemap) => basemap.id === mode) ? mode : "osm";
    }

    function styleUrl() {
        return state.theme === "dark"
            ? "https://tiles.openfreemap.org/styles/fiord"
            : "https://tiles.openfreemap.org/styles/positron";
    }

    function setOffline(show) {
        if (state.offline === show) {
            return;
        }

        state.offline = show;
        if (offlineElement) {
            offlineElement.hidden = !show;
        }
    }

    function applyAttribution() {
        const control = state.map?.attributionControl;
        if (!control) {
            return;
        }

        if (state.attribution) {
            control.removeAttribution(state.attribution);
        }

        state.attribution = ATTRIBUTIONS[state.mode] || "";
        if (state.attribution) {
            control.addAttribution(state.attribution);
        }
    }

    /* ============================ Basemaps ============================ */

    function watchVector(layer) {
        const token = state.basemapToken;
        const started = performance.now();
        let loaded = false;
        let gl = null;
        let errors = 0;

        const markLoaded = () => {
            if (token !== state.basemapToken) {
                return;
            }

            loaded = true;
            setOffline(false);
        };

        const attach = (candidate) => {
            if (!candidate || gl === candidate) {
                return;
            }

            gl = candidate;
            ["load", "idle", "style.load"].forEach((eventName) => {
                gl.on?.(eventName, markLoaded);
            });
            gl.on?.("error", () => {
                errors += 1;
            });
        };

        attach(layer.getMaplibreMap?.());
        attach(layer._glMap);

        // The old fixed timeout could report an unavailable basemap while the
        // vector map was already visible. Keep checking and hide the notice as
        // soon as the style or its tiles are actually ready. The GL map can
        // also appear a moment after the Leaflet layer has been added.
        const watcher = window.setInterval(() => {
            if (token !== state.basemapToken) {
                window.clearInterval(watcher);
                return;
            }

            attach(layer.getMaplibreMap?.());
            attach(layer._glMap);

            if (loaded || gl?.isStyleLoaded?.() || gl?.loaded?.() || gl?.areTilesLoaded?.()) {
                markLoaded();
                window.clearInterval(watcher);
                return;
            }

            const elapsed = performance.now() - started;
            if (elapsed > 9000 && (!gl || errors > 0)) {
                setOffline(true);
            }
        }, 300);
    }

    function watchRaster(layer) {
        const token = state.basemapToken;
        const started = performance.now();
        let loaded = false;
        const tiles = [];

        if (typeof layer.eachLayer === "function") {
            layer.eachLayer((child) => tiles.push(child));
        } else {
            tiles.push(layer);
        }

        const markLoaded = () => {
            if (token !== state.basemapToken) {
                return;
            }

            loaded = true;
            setOffline(false);
        };

        tiles.forEach((tileLayer) => {
            tileLayer.on?.("tileload", markLoaded);
            tileLayer.on?.("load", markLoaded);
        });

        const watcher = window.setInterval(() => {
            if (token !== state.basemapToken) {
                window.clearInterval(watcher);
                return;
            }

            if (loaded) {
                window.clearInterval(watcher);
                return;
            }

            if (performance.now() - started > 9000) {
                setOffline(true);
            }
        }, 400);
    }

    function createVectorBasemap() {
        if (!window.L?.maplibreGL || !state.map) {
            setOffline(true);
            return null;
        }

        const layer = L.maplibreGL({
            style: styleUrl(),
            attributionControl: false,
            interactive: false,
        });

        layer.addTo(state.map);
        watchVector(layer);
        state.cluster?.bringToFront?.();
        return layer;
    }

    function createOsmBasemap() {
        const layer = L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
        });

        layer.addTo(state.map);
        watchRaster(layer);
        return layer;
    }

    function createSatelliteBasemap() {
        // Esri World Imagery is free to use with attribution and does not need
        // an API key. Boundaries/labels are added as a second transparent layer.
        const imagery = L.tileLayer(
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
            { maxZoom: 19 });
        const labels = L.tileLayer(
            "https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}",
            { maxZoom: 19, opacity: 0.9 });

        const group = L.layerGroup([imagery, labels]).addTo(state.map);
        watchRaster(group);
        return group;
    }

    function createReliefBasemap() {
        const layer = L.tileLayer("https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png", {
            subdomains: ["a", "b", "c"],
            maxNativeZoom: 17,
            maxZoom: 19,
        });

        layer.addTo(state.map);
        watchRaster(layer);
        return layer;
    }

    function createBasemap() {
        switch (state.mode) {
            case "osm":
                return createOsmBasemap();
            case "satellite":
                return createSatelliteBasemap();
            case "relief":
                return createReliefBasemap();
            default:
                return createVectorBasemap();
        }
    }

    function removeBasemap() {
        state.basemapToken += 1;
        if (!state.basemap || !state.map) {
            state.basemap = null;
            return;
        }

        try {
            state.map.removeLayer(state.basemap);
        } catch {
            // Ignore.
        }

        state.basemap = null;
    }

    /* ============================ Layers control ============================ */

    function layerPreviewHtml(basemap) {
        if (basemap.id === "plan") {
            return `
                <img class="cf-layer__preview-light" src="${basemap.previewLight}" alt="">
                <img class="cf-layer__preview-dark" src="${basemap.previewDark}" alt="">`;
        }

        return `<img src="${basemap.preview}" alt="">`;
    }

    function layerChoicesHtml() {
        return BASEMAPS.map((basemap) => `
            <button type="button" class="cf-layer" data-layer="${basemap.id}"
                    aria-pressed="false" title="${escapeHtml(basemap.name)}">
                <span class="cf-layer__preview">
                    ${layerPreviewHtml(basemap)}
                </span>
                <span class="cf-layer__text">
                    <strong>${escapeHtml(basemap.name)}</strong>
                    <small>${escapeHtml(basemap.detail)}</small>
                </span>
                <span class="cf-layer__check" aria-hidden="true">✓</span>
            </button>`).join("");
    }

    function createLayersControl() {
        if (!state.map) {
            return;
        }

        const control = L.control({ position: "bottomleft" });
        control.onAdd = () => {
            const container = L.DomUtil.create("div", "leaflet-control cf-layers");
            container.innerHTML = `
                <button type="button" class="cf-layers__toggle" aria-expanded="false"
                        title="Choisir le fond de carte">
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                        <path d="M12 3 2 8l10 5 10-5-10-5Z M2 12l10 5 10-5 M2 16l10 5 10-5"/>
                    </svg>
                    <span>Calque</span>
                </button>
                <div class="cf-layers__panel" hidden>
                    <div class="cf-layers__head">
                        <strong>Fond de carte</strong>
                        <button type="button" class="cf-layers__close" aria-label="Fermer le menu des calques">×</button>
                    </div>
                    <div class="cf-layers__list">${layerChoicesHtml()}</div>
                    <p class="cf-layers__note">Fonds libres, sans clé API.</p>
                </div>`;

            const toggle = container.querySelector(".cf-layers__toggle");
            const panel = container.querySelector(".cf-layers__panel");
            const close = container.querySelector(".cf-layers__close");

            const setOpen = (open) => {
                panel.hidden = !open;
                toggle.setAttribute("aria-expanded", String(open));
            };

            toggle.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
                setOpen(panel.hidden);
            });

            close.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
                setOpen(false);
            });

            container.querySelectorAll("[data-layer]").forEach((button) => {
                button.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    setBasemap(button.dataset.layer);
                    setOpen(false);
                });
            });

            document.addEventListener("click", (event) => {
                if (!panel.hidden && !container.contains(event.target)) {
                    setOpen(false);
                }
            });

            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape" && !panel.hidden) {
                    setOpen(false);
                }
            });

            L.DomEvent.disableClickPropagation(container);
            L.DomEvent.disableScrollPropagation(container);

            state.layersContainer = container;
            return container;
        };

        control.addTo(state.map);
    }

    function syncLayerChoices() {
        const container = state.layersContainer;
        if (!container) {
            return;
        }

        container.querySelectorAll("[data-layer]").forEach((button) => {
            const active = button.dataset.layer === state.mode;
            button.classList.toggle("is-active", active);
            button.setAttribute("aria-pressed", String(active));
        });
    }

    /* ============================ Markers ============================ */

    function markerIcon(point) {
        const classes = [
            "cf-marker",
            point.active ? "is-active" : "",
            point.selected ? "is-selected" : "",
        ].filter(Boolean).join(" ");

        const thumbnail = state.thumbnails.get(point.id);
        const visual = thumbnail
            ? `<img src="${thumbnail}" alt="">`
            : `<svg class="cf-marker__camera" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M4 8h3l1.5-2h7L17 8h3v11H4V8z"/>
                    <circle cx="12" cy="13.5" r="3"/>
               </svg>`;

        return L.divIcon({
            className: "cf-marker-wrap",
            html: `<div class="${classes}" title="${escapeHtml(point.name)}">${visual}</div>`,
            iconSize: [40, 40],
            iconAnchor: [20, 20],
            popupAnchor: [0, -22],
        });
    }

    function clusterIcon(cluster) {
        const count = cluster.getChildCount();
        return L.divIcon({
            className: "cf-cluster-wrap",
            html: `<div class="cf-cluster" title="${count} photos — cliquez pour afficher">${count}</div>`,
            iconSize: [42, 42],
            iconAnchor: [21, 21],
        });
    }

    function popupHtml(point) {
        const city = point.city?.trim() || "Ville à confirmer";
        const text = point.text?.trim();
        const lat = Number(point.lat).toFixed(5);
        const lon = Number(point.lon).toFixed(5);
        const thumbnail = state.thumbnails.get(point.id);

        return `
            <div class="cf-popup">
                <div class="cf-popup__thumb">
                    ${thumbnail ? "" : '<span class="cf-popup__thumb-empty">Aperçu…</span>'}
                    <img ${thumbnail ? `src="${thumbnail}"` : `data-cf-thumb="${escapeHtml(point.id)}"`}
                         alt="" loading="lazy">
                </div>
                <div class="cf-popup__body">
                    <div class="cf-popup__top">
                        <span class="cf-popup__status">${escapeHtml(point.status || "Photo")}</span>
                        <span class="cf-popup__date">${escapeHtml(point.date || "")}</span>
                    </div>
                    <strong class="cf-popup__name">${escapeHtml(point.name)}</strong>
                    <div class="cf-popup__city">${escapeHtml(city)}${text ? ` <span class="cf-popup__text">· ${escapeHtml(text)}</span>` : ""}</div>
                    <div class="cf-popup__meta">${lat}, ${lon}</div>
                    <div class="cf-popup__actions">
                        <button type="button" class="cf-popup__button" data-cf-focus>Grille</button>
                        <button type="button" class="cf-popup__button cf-popup__button--primary" data-cf-details>Voir la fiche</button>
                    </div>
                </div>
            </div>`;
    }

    function requestThumbnail(id) {
        if (!id || state.thumbnails.has(id) || state.requestedThumbs.has(id)) {
            return;
        }

        state.requestedThumbs.add(id);
        post({ type: "requestThumbnail", id });
    }

    function requestVisibleMarkerThumbnails() {
        if (!state.cluster) {
            return;
        }

        const canTellVisibleMarkers = typeof state.cluster.getVisibleParent === "function";
        state.markers.forEach((marker, id) => {
            // Only individual, rendered markers need a preview. Markers hidden
            // inside a cluster are requested when the cluster is spiderfied.
            if (!canTellVisibleMarkers || state.cluster.getVisibleParent(marker) === marker) {
                requestThumbnail(id);
            }
        });
    }

    function spiderfyShapePositions(count, center) {
        const spacing = 48;
        const radius = Math.max(54, spacing * count / (2 * Math.PI));
        const positions = [];

        for (let index = 0; index < count; index += 1) {
            const angle = -Math.PI / 2 + (2 * Math.PI * index) / count;
            positions.push(L.point(
                center.x + radius * Math.cos(angle),
                center.y + radius * Math.sin(angle)).round());
        }

        return positions;
    }

    function bindPopupActions(marker, point) {
        const popupElement = marker.getPopup()?.getElement();
        if (!popupElement) {
            return;
        }

        popupElement.querySelector("[data-cf-focus]")?.addEventListener("click", () => {
            post({ type: "photoFocused", id: point.id });
        }, { once: true });

        popupElement.querySelector("[data-cf-details]")?.addEventListener("click", () => {
            post({ type: "showPhotoDetails", id: point.id });
        }, { once: true });

        requestThumbnail(point.id);
    }

    function setThumbnail(id, dataUrl) {
        if (!id || typeof dataUrl !== "string" || !dataUrl.startsWith("data:image/")) {
            return;
        }
        state.thumbnails.set(id, dataUrl);
        document.querySelectorAll("img[data-cf-thumb]").forEach((image) => {
            if (image.dataset.cfThumb !== id) {
                return;
            }

            image.src = dataUrl;
            image.removeAttribute("data-cf-thumb");
            image.previousElementSibling?.remove();
        });

        const point = state.pointsById.get(id);
        const marker = state.markers.get(id);
        if (point && marker) {
            marker.setIcon(markerIcon(point));
        }
    }

    function addMarker(point) {
        const marker = L.marker([point.lat, point.lon], {
            icon: markerIcon(point),
            title: point.name,
            riseOnHover: true,
            keyboard: true,
            autoPanOnFocus: true,
        });

        marker.bindPopup(popupHtml(point), {
            maxWidth: 270,
            minWidth: 238,
            closeButton: true,
            autoPanPadding: [24, 24],
            className: "cf-popup-shell",
        });

        marker.on("click", () => {
            post({ type: "photoFocused", id: point.id });
        });

        marker.on("popupopen", () => {
            bindPopupActions(marker, point);
        });


        marker.cfId = point.id;
        state.markers.set(point.id, marker);
        state.cluster?.addLayer(marker);
    }

    function fitAll() {
        if (!state.map || state.points.length === 0) {
            return;
        }

        state.map.invalidateSize();
        state.fitting = true;
        window.setTimeout(() => {
            state.fitting = false;
        }, 650);

        if (state.points.length === 1) {
            state.map.setView([state.points[0].lat, state.points[0].lon], 13, {
                animate: !reduceMotion,
            });
            return;
        }

        const bounds = L.latLngBounds(state.points.map((point) => [point.lat, point.lon]));
        state.map.fitBounds(bounds, {
            padding: [38, 38],
            maxZoom: 12,
            animate: !reduceMotion,
        });
    }

    function setPhotos(points, fit) {
        if (!state.map || !state.cluster) {
            return;
        }

        const normalized = Array.isArray(points)
            ? points.filter((point) => {
                const lat = Number(point?.lat);
                const lon = Number(point?.lon);
                return Number.isFinite(lat) && Number.isFinite(lon) &&
                    Math.abs(lat) <= 90 && Math.abs(lon) <= 180;
            }).map((point) => ({
                ...point,
                lat: Number(point.lat),
                lon: Number(point.lon),
            }))
            : [];

        const sameSet = normalized.length === state.markers.size &&
            normalized.every((point) => {
                const marker = state.markers.get(point.id);
                if (!marker) {
                    return false;
                }

                // While a cluster is spiderfied, getLatLng() returns the
                // temporary flower position; compare against the original
                // coordinate so a routine setPhotos() does not rebuild the
                // layer and collapse the open spiderfy/popup.
                const originalLatLng = marker._preSpiderfyLatlng ?? marker.getLatLng();
                return originalLatLng.equals(L.latLng(point.lat, point.lon));
            });
        state.points = normalized;
        state.pointsById = new Map(normalized.map((point) => [point.id, point]));

        if (sameSet) {
            // Selection, city or text changes must not close an open popup or
            // rebuild the whole cluster layer.
            normalized.forEach((point) => {
                const marker = state.markers.get(point.id);
                if (!marker) {
                    return;
                }

                marker.setIcon(markerIcon(point));
                marker.setPopupContent(popupHtml(point));
                marker.options.title = point.name;
                marker.getElement()?.setAttribute("title", point.name);
                if (marker.isPopupOpen()) {
                    bindPopupActions(marker, point);
                }
            });
        } else {
            state.cluster.clearLayers();
            state.markers.clear();
            normalized.forEach(addMarker);
        }

        requestVisibleMarkerThumbnails();

        if (emptyElement) {
            emptyElement.hidden = normalized.length > 0;
        }

        if (fit) {
            state.autoFit = true;
        }

        if ((fit || !state.hasFitted) && normalized.length > 0) {
            window.setTimeout(() => {
                state.map?.invalidateSize();
                fitAll();
            }, 60);
            state.hasFitted = true;
        }

        window.setTimeout(() => state.map?.invalidateSize(), 30);
    }

    function focusPhoto(id) {
        if (!state.map) {
            return;
        }

        const marker = state.markers.get(id);
        if (!marker) {
            return;
        }

        // A marker that is already rendered (including one opened by a
        // spiderfy) only needs its popup. Forcing a zoom here would make
        // Leaflet.markercluster unspiderfy and close the popup.
        const visibleParent = typeof state.cluster?.getVisibleParent === "function"
            ? state.cluster.getVisibleParent(marker)
            : marker;
        if (marker._icon &&
            visibleParent === marker &&
            state.map.getBounds().contains(marker.getLatLng())) {
            marker.openPopup();
            return;
        }

        const open = () => {
            state.map.setView(marker.getLatLng(), Math.max(state.map.getZoom(), 13), {
                animate: !reduceMotion,
            });
            marker.openPopup();
        };

        if (state.cluster && typeof state.cluster.zoomToShowLayer === "function") {
            state.cluster.zoomToShowLayer(marker, open);
        } else {
            open();
        }
    }

    function setTheme(theme) {
        const next = theme === "dark" ? "dark" : "light";
        const changed = state.theme !== next;
        state.theme = next;
        document.documentElement.dataset.theme = next;

        if (changed && state.mode === "plan" && state.map) {
            removeBasemap();
            setOffline(false);
            state.basemap = createBasemap();
            applyAttribution();
        }

        window.setTimeout(() => state.map?.invalidateSize(), 30);
    }

    function setBasemap(mode) {
        const next = normalizeMode(mode);
        document.documentElement.dataset.basemap = next;

        if (next === state.mode && state.basemap) {
            syncLayerChoices();
            applyAttribution();
            return;
        }

        state.mode = next;
        removeBasemap();
        setOffline(false);

        if (state.map) {
            state.basemap = createBasemap();
            applyAttribution();
            syncLayerChoices();
        }

        window.setTimeout(() => state.map?.invalidateSize(), 30);
        post({ type: "basemapChanged", mode: state.mode });
    }

    function init() {
        if (!window.L || !mapElement) {
            setOffline(true);
            post({ type: "ready" });
            return;
        }

        try {
            state.map = L.map(mapElement, {
                zoomControl: false,
                attributionControl: true,
                minZoom: 2,
                maxZoom: 19,
                worldCopyJump: true,
                zoomSnap: 0.5,
                wheelPxPerZoomLevel: 120,
            });

            state.map.attributionControl.setPrefix(false);

            L.control.zoom({
                position: "topright",
                zoomInTitle: "Zoomer",
                zoomOutTitle: "Dézoomer",
            }).addTo(state.map);

            const hasCluster = typeof L.markerClusterGroup === "function";
            state.cluster = hasCluster
                ? L.markerClusterGroup({
                    showCoverageOnHover: false,
                    zoomToBoundsOnClick: false,
                    spiderfyOnMaxZoom: false,
                    removeOutsideVisibleBounds: true,
                    animate: !reduceMotion,
                    maxClusterRadius: 46,
                    spiderfyDistanceMultiplier: 1.8,
                    spiderfyShapePositions,
                    spiderLegPolylineOptions: {
                        weight: 1.2,
                        color: "#7A8C80",
                        opacity: 0.5,
                    },
                    iconCreateFunction: clusterIcon,
                })
                : L.layerGroup();

            state.cluster.addTo(state.map);

            if (hasCluster) {
                state.cluster.on("clusterclick", (event) => {
                    const cluster = event.layer;
                    const count = cluster.getChildCount();
                    const bounds = cluster.getBounds();
                    const targetZoom = state.map.getBoundsZoom(bounds, false, [60, 60]);

                    // Small clusters open as circular photo markers. Larger
                    // clusters zoom first to avoid an unreadable flower.
                    if (count <= 12 || targetZoom <= state.map.getZoom()) {
                        cluster.spiderfy();
                    } else {
                        state.map.fitBounds(bounds, {
                            padding: [40, 40],
                            maxZoom: state.map.getMaxZoom(),
                            animate: !reduceMotion,
                        });
                    }
                });

                state.cluster.on("spiderfied", (event) => {
                    event.markers.forEach((marker) => requestThumbnail(marker.cfId));

                    // Keep a flower opened near the edge of the panel visible.
                    window.setTimeout(() => {
                        if (!state.map || event.markers.length === 0) {
                            return;
                        }

                        // Do not move the map under a popup the user just
                        // opened, and do not pan after the flower was closed.
                        if (event.markers.some((marker) => marker.isPopupOpen()) ||
                            (state.cluster?._spiderfied &&
                             state.cluster._spiderfied !== event.cluster)) {
                            return;
                        }

                        const bounds = L.latLngBounds(
                            event.markers.map((marker) => marker.getLatLng()));
                        state.map.panInsideBounds(bounds, {
                            padding: [36, 36],
                            animate: !reduceMotion,
                        });
                    }, 240);
                });

                state.cluster.on("animationend", requestVisibleMarkerThumbnails);
            }

            document.documentElement.dataset.basemap = state.mode;
            createLayersControl();
            state.basemap = createBasemap();
            applyAttribution();
            syncLayerChoices();
            state.map.setView([46.6, 2.4], 6);

            state.map.on("dragstart", () => {
                state.autoFit = false;
            });
            state.map.on("zoomstart", () => {
                if (!state.fitting) {
                    state.autoFit = false;
                }
            });
            state.map.on("zoomend", requestVisibleMarkerThumbnails);
            state.map.on("moveend", requestVisibleMarkerThumbnails);

            if (typeof ResizeObserver === "function") {
                let resizeTimer = 0;
                new ResizeObserver(() => {
                    window.clearTimeout(resizeTimer);
                    resizeTimer = window.setTimeout(() => {
                        state.map?.invalidateSize();
                        if (state.autoFit && state.points.length > 0) {
                            fitAll();
                        }
                    }, 160);
                }).observe(mapElement);
            }

            window.setTimeout(() => {
                state.map?.invalidateSize();
                post({ type: "ready" });
            }, 120);
        } catch (error) {
            console.error("Carte indisponible", error);
            setOffline(true);
            post({ type: "ready" });
        }
    }

    window.cityfinder = {
        setPhotos,
        focusPhoto,
        fitAll,
        setTheme,
        setBasemap,
        setThumbnail,
    };

    init();
})();
