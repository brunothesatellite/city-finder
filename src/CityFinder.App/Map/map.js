(() => {
    "use strict";

    const mapElement = document.getElementById("map");
    const offlineElement = document.getElementById("map-offline");
    const emptyElement = document.getElementById("map-empty");
    const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;

    const state = {
        map: null,
        cluster: null,
        basemap: null,
        markers: new Map(),
        points: [],
        activeId: null,
        theme: document.documentElement.dataset.theme === "dark" ? "dark" : "light",
        hasFitted: false,
        autoFit: true,
        fitting: false,
        offline: false,
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

    function createBasemap() {
        if (!window.L?.maplibreGL || !state.map) {
            setOffline(true);
            return null;
        }

        const layer = L.maplibreGL({
            style: styleUrl(),
            attributionControl: true,
            interactive: false,
        });

        layer.addTo(state.map);
        const started = performance.now();
        const watcher = window.setInterval(() => {
            if (state.basemap !== layer) {
                window.clearInterval(watcher);
                return;
            }

            const gl = layer.getMaplibreMap?.();
            if (gl?.isStyleLoaded?.()) {
                setOffline(false);
                window.clearInterval(watcher);
                return;
            }

            if (performance.now() - started > 4800) {
                setOffline(true);
                window.clearInterval(watcher);
            }
        }, 320);

        layer._cfWatcher = watcher;
        state.cluster?.bringToFront?.();
        return layer;
    }

    function removeBasemap() {
        if (!state.basemap || !state.map) {
            return;
        }

        window.clearInterval(state.basemap._cfWatcher);
        try {
            state.map.removeLayer(state.basemap);
        } catch {
            // Ignore.
        }

        state.basemap = null;
    }

    function markerIcon(point) {
        const classes = [
            "cf-marker",
            point.active ? "is-active" : "",
            point.selected ? "is-selected" : "",
        ].filter(Boolean).join(" ");

        return L.divIcon({
            className: "cf-marker-wrap",
            html: `<div class="${classes}" title="${escapeHtml(point.name)}"><span></span></div>`,
            iconSize: [22, 22],
            iconAnchor: [11, 11],
        });
    }

    function clusterIcon(cluster) {
        const count = cluster.getChildCount();
        return L.divIcon({
            className: "cf-cluster-wrap",
            html: `<div class="cf-cluster" title="${count} photos — cliquez pour zoomer">${count}</div>`,
            iconSize: [42, 42],
            iconAnchor: [21, 21],
        });
    }

    function popupHtml(point) {
        const city = point.city?.trim() || "Ville à confirmer";
        const text = point.text?.trim();
        const lat = Number(point.lat).toFixed(5);
        const lon = Number(point.lon).toFixed(5);

        return `
            <div class="cf-popup">
                <div class="cf-popup__top">
                    <span class="cf-popup__status">${escapeHtml(point.status || "Photo")}</span>
                    <span class="cf-popup__date">${escapeHtml(point.date || "")}</span>
                </div>
                <strong class="cf-popup__name">${escapeHtml(point.name)}</strong>
                <div class="cf-popup__city">${escapeHtml(city)}${text ? ` <span class="cf-popup__text">· ${escapeHtml(text)}</span>` : ""}</div>
                <div class="cf-popup__meta">${lat}, ${lon}</div>
                <button type="button" class="cf-popup__button" data-cf-focus>Voir dans la grille</button>
            </div>`;
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

        marker.on("popupopen", (event) => {
            const button = event.popup.getElement()?.querySelector("[data-cf-focus]");
            button?.addEventListener("click", () => {
                post({ type: "photoFocused", id: point.id });
            }, { once: true });
        });

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
                return marker && marker.getLatLng().equals(L.latLng(point.lat, point.lon));
            });

        state.points = normalized;

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
            });
        } else {
            state.cluster.clearLayers();
            state.markers.clear();
            normalized.forEach(addMarker);
        }

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
        if (state.theme !== next) {
            state.theme = next;
            document.documentElement.dataset.theme = next;
            removeBasemap();
            state.basemap = createBasemap();
            window.setTimeout(() => state.map?.invalidateSize(), 30);
        } else {
            document.documentElement.dataset.theme = next;
        }
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
                maxZoom: 18,
                worldCopyJump: true,
                zoomSnap: 0.5,
                wheelPxPerZoomLevel: 120,
            });

            state.map.attributionControl.setPrefix(false);
            state.map.attributionControl.addAttribution(
                '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors · ' +
                '<a href="https://openfreemap.org/" target="_blank" rel="noopener noreferrer">OpenFreeMap</a> · ' +
                '<a href="https://www.openmaptiles.org/" target="_blank" rel="noopener noreferrer">OpenMapTiles</a>'
            );

            L.control.zoom({
                position: "topright",
                zoomInTitle: "Zoomer",
                zoomOutTitle: "Dézoomer",
            }).addTo(state.map);

            const hasCluster = typeof L.markerClusterGroup === "function";
            state.cluster = hasCluster
                ? L.markerClusterGroup({
                    showCoverageOnHover: false,
                    zoomToBoundsOnClick: true,
                    spiderfyOnMaxZoom: true,
                    removeOutsideVisibleBounds: true,
                    animate: !reduceMotion,
                    maxClusterRadius: 46,
                    spiderfyDistanceMultiplier: 1.3,
                    iconCreateFunction: clusterIcon,
                })
                : L.layerGroup();

            state.cluster.addTo(state.map);
            state.basemap = createBasemap();
            state.map.setView([46.6, 2.4], 6);

            state.map.on("dragstart", () => {
                state.autoFit = false;
            });
            state.map.on("zoomstart", () => {
                if (!state.fitting) {
                    state.autoFit = false;
                }
            });

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
    };

    init();
})();
