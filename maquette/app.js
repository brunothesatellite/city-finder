/* ============================================================
   CityFinder — maquette interactive
   Aucune donnée n'est envoyée : les villes sont précalculées.
   Seules les tuiles de la carte sont chargées depuis OSM.
   ============================================================ */

(() => {
  "use strict";

  const DATA = window.CITYFINDER_DATA;
  if (!DATA) {
    document.body.innerHTML = "<p style='padding:2rem;font-family:sans-serif'>Données de démonstration introuvables.</p>";
    return;
  }

  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const mobileMapQuery = window.matchMedia("(max-width: 1099px)");
  const SESSION_KEY = "cityfinder-demo-session-v3";
  const SESSION_VERSION = 3;

  const KNOWN_CITIES = Array.from(new Set(
    DATA.photos.map((photo) => normalizeCity(photo.detectedCity)).filter(Boolean)
  )).sort((a, b) => b.length - a.length);

  // Quelques noms pré-renommés pour montrer la détection d'une ville déjà
  // présente dans le fichier, y compris un cas différent de Photon.
  const DEMO_EXISTING = DATA.demoExisting || {};

  const els = {
    views: {
      welcome: $("#view-welcome"),
      scan: $("#view-scan"),
      results: $("#view-results"),
      success: $("#view-success"),
    },
    folderPill: $("#folder-pill"),
    folderPillName: $("#folder-pill-name"),
    folderPillCount: $("#folder-pill-count"),
    scanFolderName: $("#scan-folder-name"),
    resultsFolderName: $("#results-folder-name"),
    folderDialogPath: $("#folder-dialog-path span"),
    folderDialogName: $("#folder-dialog-name"),
    folderDialogMeta: $("#folder-dialog-meta"),
    localPill: $("#local-pill"),
    welcomeResume: $("#welcome-resume"),
    welcomeResumeText: $("#welcome-resume-text"),
    themeToggle: $("#theme-toggle"),
    mapToggle: $("#map-toggle"),
    mapPanel: $("#map-panel"),
    mapScrim: $("#map-scrim"),
    mapCanvas: $("#map-canvas"),
    mapEmpty: $("#map-empty"),
    mapOffline: $("#map-offline"),
    mapPointCount: $("#map-point-count"),
    mapCityCount: $("#map-city-count"),
    workspace: $("#results-workspace"),
    photoScroll: $("#photo-scroll"),
    photoGrid: $("#photo-grid"),
    emptyState: $("#empty-state"),
    listFootnote: $(".list-footnote"),
    searchInput: $("#search-input"),
    sortSelect: $("#sort-select"),
    cityChips: $("#city-chips"),
    selectAll: $("#select-all"),
    selectionNote: $("#selection-note"),
    renameButton: $("#rename-button"),
    renameButtonLabel: $("#rename-button-label"),
    undoBanner: $("#undo-banner"),
    undoBannerText: $("#undo-banner strong"),
    resumeBanner: $("#resume-banner"),
    resumeSummary: $("#resume-summary"),
    existingSummary: $("#existing-summary"),
    existingSummaryText: $("#existing-summary-text"),
    tabs: $$(".tab"),
    resultsTitle: $("#results-title"),
    renameDialog: $("#rename-dialog"),
    renameDialogCard: $("#rename-dialog .modal__card"),
    renameDialogCount: $("#rename-dialog-count"),
    renameSummaryFiles: $("#rename-summary-files"),
    renameSummaryCities: $("#rename-summary-cities"),
    renameSummaryIgnored: $("#rename-summary-ignored"),
    renameIgnoredText: $("#rename-ignored-text"),
    renameExampleList: $("#rename-example-list"),
    confirmRename: $("#confirm-rename"),
    confirmRenameLabel: $("#confirm-rename-label"),
    renameProgress: $("#rename-progress"),
    renameProgressCount: $("#rename-progress-count"),
    renameProgressBar: $("#rename-progress-bar"),
    detailDialog: $("#detail-dialog"),
    detailImage: $("#detail-image"),
    detailStatusBadge: $("#detail-status-badge"),
    detailEyebrow: $("#detail-eyebrow"),
    detailTitle: $("#detail-title"),
    detailSub: $("#detail-sub"),
    detailCity: $("#detail-city"),
    detailCityReset: $("#detail-city-reset"),
    detailCityHint: $("#detail-city-hint"),
    detailExisting: $("#detail-existing"),
    detailUsePhoton: $("#detail-use-photon"),
    detailPhotonCity: $("#detail-photon-city"),
    detailPhotonConfirm: $("#detail-photon-confirm"),
    detailText: $("#detail-text"),
    detailTextReset: $("#detail-text-reset"),
    detailCoords: $("#detail-coords"),
    detailDate: $("#detail-date"),
    detailDevice: $("#detail-device"),
    detailDimensions: $("#detail-dimensions"),
    detailRegion: $("#detail-region"),
    detailDetected: $("#detail-detected"),
    detailNewName: $("#detail-newname"),
    detailOsmLink: $("#detail-osm-link"),
    detailMapButton: $("#detail-map-button"),
    detailMissingNote: $("#detail-missing-note"),
    detailSelected: $("#detail-selected"),
    folderDialog: $("#folder-dialog"),
    aboutDialog: $("#about-dialog"),
    scanRing: $("#scan-ring"),
    scanPercent: $("#scan-percent"),
    scanStageShort: $("#scan-stage-short"),
    scanMetaDetail: $("#scan-meta-detail"),
    scanMetaValue: $("#scan-meta-value"),
    scanPlacesValue: $("#scan-places-value"),
    scanThumbsValue: $("#scan-thumbs-value"),
    scanLogList: $("#scan-log-list"),
    scanLogCount: $("#scan-log-count"),
    successCount: $("#success-count"),
    successCities: $("#success-cities"),
    successExamples: $("#success-examples"),
    toastRegion: $("#toast-region"),
    liveRegion: $("#live-region"),
    scanStages: $$(".scan-stage"),
  };

  const state = {
    photos: [],
    byId: new Map(),
    filter: "all",
    cityFilter: "all",
    search: "",
    sort: "date-desc",
    scanned: false,
    activeId: null,
    scanTimer: null,
    scanStart: 0,
    searchTimer: null,
    renaming: false,
    renames: [],
    priorRenames: [],
    sessionEntries: new Map(),
    mapOpen: !mobileMapQuery.matches,
    map: null,
    clusterGroup: null,
    markers: new Map(),
    mapInitialized: false,
    mapTileLayer: null,
    mapTileLoads: 0,
    mapTileErrors: 0,
    mapOfflineShown: false,
    hasClusterPlugin: false,
    detailId: null,
    detailDirty: false,
    detailCloseToMap: false,
    currentPlan: null,
    resizeTimer: null,
  };

  /* ------------------------------------------------------------
     Utilitaires
     ------------------------------------------------------------ */

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (char) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;",
    }[char]));
  }

  function normalizeCity(value) {
    return String(value ?? "").replace(/\s+/g, " ").trim();
  }

  function fold(value) {
    return normalizeCity(value)
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLocaleLowerCase("fr");
  }

  function sanitizeCity(value) {
    return normalizeCity(value)
      .replace(/[<>:"/\\|?*\u0000-\u001f]/g, "")
      .replace(/[. ]+$/g, "")
      .slice(0, 80)
      .trim();
  }

  function extensionOf(name) {
    const dot = name.lastIndexOf(".");
    return dot > 0 ? name.slice(dot) : "";
  }

  function baseOf(name) {
    const ext = extensionOf(name);
    return ext ? name.slice(0, -ext.length) : name;
  }

  function sanitizeUserText(value) {
    return normalizeCity(value)
      .replace(/[<>:"/\\|?*\u0000-\u001f]/g, "")
      .replace(/[. ]+$/g, "")
      .slice(0, 80)
      .trim();
  }

  function parseExistingSuffix(name) {
    const base = baseOf(name);
    const index = base.lastIndexOf(" - ");
    if (index <= 0) return null;
    const stem = base.slice(0, index).trimEnd();
    const suffix = base.slice(index + 3).trim();
    if (!stem || !suffix) return null;
    const city = KNOWN_CITIES
      .filter((candidate) => fold(suffix) === fold(candidate) || fold(suffix).startsWith(`${fold(candidate)} `))
      .sort((a, b) => b.length - a.length)[0];
    if (!city) return null;
    const rest = suffix.slice(city.length).replace(/^\s*[,;]\s*/, "");
    return {
      stem,
      city,
      text: normalizeCity(rest),
    };
  }

  function targetName(photo) {
    const city = sanitizeCity(photo.city);
    const text = sanitizeUserText(photo.userText);
    if (!city) return photo.name;
    const stem = photo.stem || baseOf(photo.name);
    return `${stem} - ${city}${text ? ` , ${text}` : ""}${extensionOf(photo.name)}`;
  }

  function proposedName(photo) {
    return targetName(photo);
  }

  function isAlreadyNamed(photo) {
    if (!normalizeCity(photo.city)) return false;
    return fold(targetName(photo)) === fold(photo.name);
  }

  function isActionable(photo) {
    return Boolean(
      photo &&
      photo.selected &&
      normalizeCity(photo.city) &&
      !photo.renamed &&
      !isAlreadyNamed(photo)
    );
  }

  function plural(count, singular, pluralValue = `${singular}s`) {
    return count > 1 ? pluralValue : singular;
  }

  function formatCoords(lat, lon) {
    if (lat === null || lon === null || lat === undefined || lon === undefined) return "—";
    return `${Number(lat).toFixed(5)}, ${Number(lon).toFixed(5)}`;
  }

  function formatDevice(photo) {
    const parts = [photo.make, photo.model].filter(Boolean);
    return parts.length ? parts.join(" ") : "Inconnu";
  }

  function formatRegion(photo) {
    const parts = [photo.county, photo.state, photo.country].filter(Boolean);
    return parts.length ? parts.join(" · ") : "—";
  }

  function formatDate(photo) {
    return photo.dateLabel || "Date inconnue";
  }

  function osmUrl(lat, lon) {
    return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}#map=16/${lat}/${lon}`;
  }

  function announce(message) {
    if (!els.liveRegion) return;
    els.liveRegion.textContent = "";
    window.setTimeout(() => {
      els.liveRegion.textContent = message;
    }, 30);
  }

  function showToast(message, icon = "i-check-circle") {
    if (!els.toastRegion) return;
    els.toastRegion.replaceChildren();
    const toast = document.createElement("div");
    toast.className = "toast";
    toast.innerHTML = `<svg class="icon" aria-hidden="true"><use href="#${icon}"></use></svg><span></span>`;
    $("span", toast).textContent = message;
    els.toastRegion.appendChild(toast);
    window.setTimeout(() => {
      toast.style.transition = "opacity .2s, transform .2s";
      toast.style.opacity = "0";
      toast.style.transform = "translateY(4px)";
      window.setTimeout(() => toast.remove(), 230);
    }, 4200);
  }

  function setTheme(theme, persist = true) {
    const value = theme === "dark" ? "dark" : "light";
    document.documentElement.dataset.theme = value;
    els.themeToggle?.setAttribute("aria-label", value === "dark" ? "Passer au thème clair" : "Passer au thème sombre");
    els.themeToggle?.setAttribute("title", value === "dark" ? "Thème clair" : "Thème sombre");
    if (persist) {
      try {
        localStorage.setItem("cityfinder-theme", value);
      } catch (_) { /* stockage local indisponible */ }
    }
    updateMapBasemap();
  }

  function initTheme() {
    let stored = null;
    try {
      stored = localStorage.getItem("cityfinder-theme");
    } catch (_) { /* ignoré */ }
    const params = new URLSearchParams(window.location.search);
    const requested = params.get("theme");
    const preferred = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    setTheme(requested === "dark" || requested === "light" ? requested : (stored || preferred), false);
  }

  /* ------------------------------------------------------------
     Données
     ------------------------------------------------------------ */

  function resetPhotos() {
    state.photos = DATA.photos.map((photo, index) => {
      const preset = DEMO_EXISTING[photo.name];
      const parsed = preset
        ? { stem: baseOf(photo.name), city: preset.city, text: preset.text || "" }
        : parseExistingSuffix(photo.name);
      const detectedCity = normalizeCity(photo.detectedCity);
      const existingCity = parsed ? normalizeCity(parsed.city) : "";
      const existingText = parsed ? normalizeCity(parsed.text) : "";
      const stem = parsed?.stem || baseOf(photo.name);
      const originalName = parsed
        ? `${stem} - ${existingCity}${existingText ? ` , ${existingText}` : ""}${extensionOf(photo.name)}`
        : photo.name;
      const originalCity = existingCity || detectedCity;
      return {
        ...photo,
        name: originalName,
        index,
        city: originalCity,
        detectedCity,
        originalCity,
        originalText: existingText,
        userText: existingText,
        existingCity,
        existingText,
        stem,
        selected: Boolean(originalCity) && !existingCity,
        modified: false,
        renamed: false,
        renamedFrom: null,
        renamedTo: null,
        originalName: originalName,
      };
    });
    state.byId = new Map(state.photos.map((photo) => [photo.id, photo]));
    state.activeId = null;
    state.renames = [];
    state.priorRenames = [];
    state.sessionEntries = new Map();
  }

  function sessionKey() {
    return `${SESSION_KEY}:${DATA.demoId || DATA.folder}`;
  }

  function readStoredSession() {
    try {
      const raw = localStorage.getItem(sessionKey());
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      if (!parsed || parsed.version !== SESSION_VERSION || !Array.isArray(parsed.entries)) return null;
      return parsed;
    } catch (_) {
      return null;
    }
  }

  function writeStoredSession() {
    try {
      const entries = Array.from(state.sessionEntries.values());
      if (!entries.length) {
        localStorage.removeItem(sessionKey());
      } else {
        localStorage.setItem(sessionKey(), JSON.stringify({
          version: SESSION_VERSION,
          folder: DATA.folder,
          savedAt: new Date().toISOString(),
          entries,
        }));
      }
    } catch (_) { /* stockage local indisponible */ }
    updateWelcomeResume();
  }

  function updateWelcomeResume() {
    if (!els.welcomeResume) return;
    let entries = state.sessionEntries?.size ? Array.from(state.sessionEntries.values()) : null;
    if (!entries) {
      const stored = readStoredSession();
      entries = stored?.entries || [];
    }
    const count = entries.length;
    els.welcomeResume.hidden = count === 0;
    if (count > 0 && els.welcomeResumeText) {
      els.welcomeResumeText.textContent = `${count} ${plural(count, "photo")} déjà renommée${count > 1 ? "s" : ""} — reprendre là où vous en étiez.`;
    }
  }

  function applySessionEntries(entries, { persist = false } = {}) {
    entries.forEach((entry) => {
      const photo = getPhoto(entry.id);
      if (!photo) return;
      if (fold(photo.name) !== fold(entry.from) && fold(photo.name) !== fold(entry.to)) return;
      photo.name = entry.to;
      photo.city = entry.city;
      photo.userText = entry.text || "";
      photo.selected = false;
      photo.modified = false;
      photo.renamed = true;
      photo.renamedFrom = entry.from;
      photo.renamedTo = entry.to;
      state.priorRenames.push(entry);
      state.sessionEntries.set(photo.id, entry);
    });
    if (persist) writeStoredSession();
  }

  function loadStoredSession() {
    const stored = readStoredSession();
    if (!stored?.entries?.length) return;
    applySessionEntries(stored.entries);
  }

  function seedDemoSession() {
    if (state.sessionEntries.size) return;
    const candidates = state.photos.filter((photo) =>
      photo.selected &&
      photo.hasGps &&
      !photo.existingCity &&
      normalizeCity(photo.city)
    ).slice(0, 12);
    const entries = candidates.map((photo) => ({
      id: photo.id,
      from: photo.name,
      to: targetName(photo),
      city: normalizeCity(photo.city),
      text: sanitizeUserText(photo.userText),
    })).filter((entry) => fold(entry.from) !== fold(entry.to));
    if (!entries.length) return;
    applySessionEntries(entries, { persist: true });
  }

  function resetDemoSession() {
    try {
      localStorage.removeItem(sessionKey());
    } catch (_) { /* ignoré */ }
    resetPhotos();
    state.filter = "all";
    state.cityFilter = "all";
    state.search = "";
    if (els.searchInput) els.searchInput.value = "";
    renderResults({ resetScroll: true });
    updateWelcomeResume();
    updateUndoBanner();
    showToast("Démo réinitialisée. Toutes les photos sont à nouveau proposées.", "i-undo");
    announce("Démo réinitialisée.");
  }

  function getPhoto(id) {
    return state.byId.get(id) || null;
  }

  function matchCurrentFilters(photo) {
    const city = normalizeCity(photo.city);
    if (state.filter === "located" && !city) return false;
    if (state.filter === "missing" && city) return false;
    if (state.filter === "modified" && !photo.modified) return false;
    if (state.filter === "renamed" && !photo.renamed) return false;
    if (state.cityFilter !== "all" && fold(city) !== fold(state.cityFilter)) return false;
    if (state.search) {
      const query = fold(state.search);
      if (!fold(photo.name).includes(query) && !fold(city).includes(query) && !fold(photo.userText).includes(query)) return false;
    }
    return true;
  }

  function visiblePhotos() {
    const list = state.photos.filter(matchCurrentFilters);
    return sortPhotos(list);
  }

  function sortPhotos(list) {
    const sorted = list.slice();
    sorted.sort((a, b) => {
      if (state.sort === "name-asc") {
        return a.name.localeCompare(b.name, "fr", { numeric: true, sensitivity: "base" });
      }
      if (state.sort === "city-asc") {
        const cityA = normalizeCity(a.city);
        const cityB = normalizeCity(b.city);
        if (!cityA && cityB) return 1;
        if (cityA && !cityB) return -1;
        return cityA.localeCompare(cityB, "fr", { sensitivity: "base" }) ||
          a.name.localeCompare(b.name, "fr", { numeric: true, sensitivity: "base" });
      }
      const ta = a.timestamp || 0;
      const tb = b.timestamp || 0;
      if (!ta && tb) return 1;
      if (ta && !tb) return -1;
      if (ta !== tb) return state.sort === "date-asc" ? ta - tb : tb - ta;
      return a.name.localeCompare(b.name, "fr", { numeric: true, sensitivity: "base" });
    });
    return sorted;
  }

  function selectedActionable() {
    return state.photos.filter(isActionable);
  }

  function renamedPhotos() {
    return state.photos.filter((photo) => photo.renamed);
  }

  function locatedPhotos() {
    return state.photos.filter((photo) => normalizeCity(photo.city));
  }

  function missingCount() {
    return state.photos.length - locatedPhotos().length;
  }

  function currentCityCount(photos = visiblePhotos()) {
    return new Set(photos.map((photo) => fold(photo.city)).filter(Boolean)).size;
  }

  /* ------------------------------------------------------------
     Navigation & thème
     ------------------------------------------------------------ */

  function showView(name) {
    Object.entries(els.views).forEach(([key, view]) => {
      view.hidden = key !== name;
      view.classList.toggle("is-active", key === name);
    });
    els.folderPill.hidden = name === "welcome";

    if (name === "welcome") {
      els.views.welcome.scrollTop = 0;
      updateWelcomeResume();
    }

    if (name === "results") {
      els.workspace.classList.toggle("is-map-closed", !state.mapOpen && !mobileMapQuery.matches);
      els.mapToggle?.setAttribute("aria-expanded", String(state.mapOpen));
      els.mapToggle?.classList.toggle("is-active", state.mapOpen);
      window.requestAnimationFrame(() => {
        if (state.mapOpen) {
          initMap(false);
          invalidateMap();
        }
      });
      window.setTimeout(() => {
        if (state.mapOpen && !state.mapInitialized) {
          initMap(false);
          invalidateMap();
        }
      }, 220);
    }

    if (name === "success") {
      els.views.success.scrollTop = 0;
    }

    updateUndoBanner();
  }

  function updateMapToggle() {
    if (!els.mapToggle) return;
    els.mapToggle.setAttribute("aria-expanded", String(state.mapOpen));
    els.mapToggle.classList.toggle("is-active", state.mapOpen);
    els.mapToggle.title = state.mapOpen ? "Masquer la carte" : "Afficher la carte";
  }

  function setMapOpen(open, { fit = false } = {}) {
    state.mapOpen = Boolean(open);
    const isMobile = mobileMapQuery.matches;

    if (isMobile) {
      els.mapPanel.classList.toggle("is-open", state.mapOpen);
      els.mapScrim.hidden = !state.mapOpen;
      els.workspace.classList.remove("is-map-closed");
      if (state.mapOpen) {
        window.setTimeout(() => {
          initMap(fit);
          invalidateMap();
        }, 310);
      }
    } else {
      els.workspace.classList.toggle("is-map-closed", !state.mapOpen);
      els.mapPanel.classList.remove("is-open");
      els.mapScrim.hidden = true;
      if (state.mapOpen) {
        window.setTimeout(() => {
          initMap(fit);
          invalidateMap();
        }, 40);
      }
    }

    updateMapToggle();
  }

  /* ------------------------------------------------------------
     Accueil
     ------------------------------------------------------------ */

  function setHeroPhotos() {
    const lookup = new Map(DATA.photos.map((photo) => [photo.name, photo]));
    $$("[data-hero-photo]").forEach((img) => {
      const photo = lookup.get(img.dataset.heroPhoto);
      if (!photo) {
        const parent = img.closest("figure, .scan-stack__card");
        if (parent) parent.hidden = true;
        return;
      }
      img.src = photo.thumb;
      img.loading = "eager";
      img.decoding = "async";
    });
  }

  function initStaticLabels() {
    if (els.folderPillName) els.folderPillName.textContent = DATA.folder;
    if (els.folderPillCount) els.folderPillCount.textContent = String(DATA.stats.total);
    if (els.scanFolderName) els.scanFolderName.textContent = DATA.folder;
    if (els.resultsFolderName) els.resultsFolderName.textContent = `· ${DATA.folder}`;
    if (els.folderDialogPath) els.folderDialogPath.textContent = DATA.folderPath || DATA.folder;
    if (els.folderDialogName) els.folderDialogName.textContent = DATA.folder;
    if (els.folderDialogMeta) els.folderDialogMeta.textContent = `${DATA.stats.total} photos · ${DATA.stats.located} géolocalisées`;
    if (els.resultsTitle) els.resultsTitle.textContent = `${DATA.stats.cities} communes, ${DATA.stats.located} photos localisées.`;
    if (els.folderPill) els.folderPill.title = DATA.folderPath || DATA.folder;
  }

  /* ------------------------------------------------------------
     Analyse simulée
     ------------------------------------------------------------ */

  const CITY_GROUPS = (() => {
    const counts = new Map();
    DATA.photos.forEach((photo) => {
      if (!photo.city) return;
      const key = photo.city;
      counts.set(key, (counts.get(key) || 0) + 1);
    });
    return Array.from(counts, ([city, count]) => ({ city, count }))
      .sort((a, b) => b.count - a.count || a.city.localeCompare(b.city, "fr"));
  })();

  const UNIQUE_PLACES = 75;

  function startScan() {
    closeDialog(els.folderDialog);
    stopScan();
    resetPhotos();
    loadStoredSession();
    state.scanned = false;
    state.currentPlan = null;
    state.activeId = null;
    state.filter = "all";
    state.cityFilter = "all";
    state.search = "";
    if (els.searchInput) els.searchInput.value = "";
    if (els.sortSelect) els.sortSelect.value = state.sort;
    updateTabsUI();
    updateUndoBanner();
    updateCounts();
    resetScanUI();
    showView("scan");
    announce("Analyse du dossier de démonstration en cours.");

    state.scanStart = performance.now();
    state.scanTimer = window.setInterval(tickScan, 60);
  }

  function resetScanUI() {
    els.scanRing.style.setProperty("--p", "0");
    els.scanPercent.textContent = "0 %";
    els.scanStageShort.textContent = "Lecture";
    els.scanMetaDetail.textContent = "110 fichiers à parcourir";
    els.scanMetaValue.textContent = "—";
    els.scanPlacesValue.textContent = `0 / ${UNIQUE_PLACES}`;
    els.scanThumbsValue.textContent = "—";
    els.scanLogCount.textContent = "0 lieu";
    els.scanLogList.innerHTML = '<li class="scan-log__empty">En attente des premières coordonnées…</li>';
    els.scanStages.forEach((stage) => stage.classList.remove("is-active", "is-done"));
    $('[data-stage="metadata"]', els.views.scan)?.classList.add("is-active");
    els.scanRing.setAttribute("role", "progressbar");
    els.scanRing.setAttribute("aria-valuemin", "0");
    els.scanRing.setAttribute("aria-valuemax", "100");
    els.scanRing.setAttribute("aria-valuenow", "0");
    els.scanRing.setAttribute("aria-label", "Progression de l’analyse");
    let revealed = 0;
    els.scanLogList._revealed = revealed;
  }

  function tickScan() {
    const elapsed = performance.now() - state.scanStart;
    const total = 4400;
    const progress = Math.min(1, elapsed / total);
    const percent = Math.round(progress * 100);
    els.scanRing.style.setProperty("--p", String(percent));
    els.scanRing.setAttribute("aria-valuenow", String(percent));
    els.scanPercent.textContent = `${percent} %`;

    const metadataStage = $('[data-stage="metadata"]', els.views.scan);
    const placesStage = $('[data-stage="places"]', els.views.scan);
    const thumbsStage = $('[data-stage="thumbs"]', els.views.scan);

    if (progress < 0.25) {
      els.scanStageShort.textContent = "Lecture";
      metadataStage?.classList.add("is-active");
      els.scanMetaDetail.textContent = `${Math.min(110, Math.round(progress / 0.25 * 110))} fichiers parcourus`;
    } else {
      metadataStage?.classList.remove("is-active");
      metadataStage?.classList.add("is-done");
      els.scanMetaDetail.textContent = "96 coordonnées · 14 sans GPS";
      els.scanMetaValue.textContent = "110 / 110";
    }

    if (progress >= 0.25 && progress < 0.9) {
      els.scanStageShort.textContent = "Villes";
      placesStage?.classList.add("is-active");
      const placesDone = Math.min(UNIQUE_PLACES, Math.round(((progress - 0.25) / 0.65) * UNIQUE_PLACES));
      els.scanPlacesValue.textContent = `${placesDone} / ${UNIQUE_PLACES}`;
      revealScanGroups(progress);
    } else if (progress >= 0.9) {
      placesStage?.classList.remove("is-active");
      placesStage?.classList.add("is-done");
      els.scanPlacesValue.textContent = `${UNIQUE_PLACES} / ${UNIQUE_PLACES}`;
      revealScanGroups(1);
      els.scanStageShort.textContent = "Aperçus";
      thumbsStage?.classList.add("is-active");
      els.scanThumbsValue.textContent = progress >= 1 ? "110 / 110" : `${Math.min(110, Math.round(((progress - 0.9) / 0.1) * 110))} / 110`;
    }

    if (progress >= 1) {
      thumbsStage?.classList.remove("is-active");
      thumbsStage?.classList.add("is-done");
      stopScan();
      window.setTimeout(finishScan, 380);
    }
  }

  function revealScanGroups(progress) {
    const target = Math.min(CITY_GROUPS.length, Math.floor(((progress - 0.26) / 0.62) * CITY_GROUPS.length) + 1);
    let revealed = els.scanLogList._revealed || 0;
    if (target <= revealed) return;
    if (revealed === 0) {
      els.scanLogList.innerHTML = "";
    }
    while (revealed < target) {
      const group = CITY_GROUPS[revealed];
      if (!group) break;
      const li = document.createElement("li");
      li.innerHTML = `<svg class="icon" aria-hidden="true"><use href="#i-check"></use></svg><span>${escapeHtml(group.city)}</span><span class="log-count">${group.count} ${plural(group.count, "photo")}</span>`;
      els.scanLogList.appendChild(li);
      revealed += 1;
    }
    els.scanLogList._revealed = revealed;
    els.scanLogCount.textContent = `${revealed} ${plural(revealed, "lieu", "lieux")}`;
    els.scanLogList.scrollTop = els.scanLogList.scrollHeight;
  }

  function stopScan() {
    if (state.scanTimer) {
      window.clearInterval(state.scanTimer);
      state.scanTimer = null;
    }
  }

  function finishScan() {
    stopScan();
    els.scanRing.style.setProperty("--p", "100");
    els.scanRing.setAttribute("aria-valuenow", "100");
    els.scanPercent.textContent = "100 %";
    els.scanStageShort.textContent = "Terminé";
    els.scanStages.forEach((stage) => {
      stage.classList.remove("is-active");
      stage.classList.add("is-done");
    });
    els.scanMetaValue.textContent = "110 / 110";
    els.scanPlacesValue.textContent = `${UNIQUE_PLACES} / ${UNIQUE_PLACES}`;
    els.scanThumbsValue.textContent = "110 / 110";
    state.scanned = true;
    renderResults({ resetScroll: true });
    showView("results");
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        if (state.mapOpen) {
          initMap(true);
          invalidateMap();
        }
      });
    });
    announce(`Analyse terminée. ${DATA.stats.located} photos localisées dans ${DATA.stats.cities} communes.`);
  }

  function cancelScan() {
    stopScan();
    state.scanned = false;
    showView("welcome");
    showToast("Analyse annulée.", "i-info");
    announce("Analyse annulée.");
  }

  /* ------------------------------------------------------------
     Résultats — rendu
     ------------------------------------------------------------ */

  function renderResults({ resetScroll = false } = {}) {
    if (state.cityFilter !== "all" && !state.photos.some((photo) => fold(photo.city) === fold(state.cityFilter))) {
      state.cityFilter = "all";
    }
    renderCityChips();
    renderGrid();
    if (resetScroll && els.photoScroll) {
      els.photoScroll.scrollTop = 0;
    }
    updateTabsUI();
    updateCounts();
    updateMapMarkers({ fit: false });
  }

  function renderCityChips() {
    if (!els.cityChips) return;
    const counts = new Map();
    state.photos.forEach((photo) => {
      const city = normalizeCity(photo.city);
      if (!city) return;
      const key = fold(city);
      if (!counts.has(key)) counts.set(key, { city, count: 0 });
      counts.get(key).count += 1;
    });
    const groups = Array.from(counts.values())
      .sort((a, b) => b.count - a.count || a.city.localeCompare(b.city, "fr"));

    const allCount = groups.reduce((sum, group) => sum + group.count, 0);
    const allActive = state.cityFilter === "all";
    els.cityChips.innerHTML = `
      <button class="city-chip ${allActive ? "is-active" : ""}" type="button" data-city="all" aria-pressed="${allActive}">
        <svg class="icon icon--xs" aria-hidden="true"><use href="#i-map"></use></svg>
        Toutes les villes <span class="city-chip__count">${allCount}</span>
      </button>
      ${groups.map((group) => {
        const active = state.cityFilter !== "all" && fold(state.cityFilter) === fold(group.city);
        return `<button class="city-chip ${active ? "is-active" : ""}" type="button" data-city="${escapeHtml(group.city)}" aria-pressed="${active}">
          ${escapeHtml(group.city)} <span class="city-chip__count">${group.count}</span>
        </button>`;
      }).join("")}
    `;
  }

  function recalcPhoto(photo) {
    const city = normalizeCity(photo.city);
    const text = normalizeCity(photo.userText);
    photo.modified = city !== normalizeCity(photo.originalCity) ||
      text !== normalizeCity(photo.originalText);
    const alreadyNamed = isAlreadyNamed(photo);
    photo.selected = Boolean(city) && !alreadyNamed && !photo.renamed;
    return alreadyNamed;
  }

  function cardTemplate(photo, index) {
    const city = normalizeCity(photo.city);
    const detected = normalizeCity(photo.detectedCity);
    const selected = photo.selected ? "checked" : "";
    const isMissing = !photo.hasGps;
    const isUnselected = !photo.selected;
    const alreadyNamed = isAlreadyNamed(photo);
    const checkboxLocked = photo.renamed || alreadyNamed;
    const classes = [
      "photo-card",
      isMissing ? "is-missing" : "",
      isUnselected ? "is-unselected" : "",
      photo.modified ? "is-modified" : "",
      photo.renamed ? "is-renamed" : "",
      alreadyNamed ? "is-already-named" : "",
      photo.existingCity ? "has-existing-city" : "",
      state.activeId === photo.id ? "is-active" : "",
    ].filter(Boolean).join(" ");
    const hitLabel = isMissing
      ? `Ouvrir la fiche de ${photo.name}`
      : `Centrer ${photo.name} sur la carte`;
    const hint = isMissing
      ? `<svg class="icon" aria-hidden="true"><use href="#i-eye"></use></svg> Voir la fiche`
      : `<svg class="icon" aria-hidden="true"><use href="#i-map"></use></svg> Voir sur la carte`;
    const checkboxTitle = photo.renamed
      ? "Photo déjà renommée dans une session précédente"
      : alreadyNamed
        ? "Nom déjà complet — modifiez la ville ou le texte pour le renommer"
        : "Inclure dans le renommage";
    const showExisting = Boolean(photo.existingCity) && !photo.modified && !photo.renamed;
    const showSuggestion = showExisting && fold(city) !== fold(detected);
    const existingNote = showExisting && fold(city) === fold(detected);
    return `
      <article class="${classes}" data-id="${photo.id}" style="--card-index:${Math.min(index, 14)}">
        <div class="card-media">
          <img src="${photo.thumb}" alt="" loading="lazy" decoding="async">
          <button class="card-hit" type="button" data-card-hit aria-label="${escapeHtml(hitLabel)}">
            <span class="media-hint">${hint}</span>
          </button>
          <label class="card-check" title="${checkboxTitle}">
            <input type="checkbox" data-select ${selected} ${checkboxLocked ? "disabled" : ""} aria-label="Sélectionner ${escapeHtml(photo.name)} pour le renommage">
            <span class="card-check__box" aria-hidden="true"><svg class="icon"><use href="#i-check"></use></svg></span>
          </label>
          <button class="card-expand" type="button" data-card-expand aria-label="Agrandir ${escapeHtml(photo.name)}" title="Voir la fiche">
            <svg class="icon" aria-hidden="true"><use href="#i-expand"></use></svg>
          </button>
          ${isMissing ? `<span class="missing-chip"><svg class="icon" aria-hidden="true"><use href="#i-alert"></use></svg> Sans GPS</span>` : ""}
        </div>
        <div class="card-body">
          <div class="card-file-row">
            <span class="card-file-name" title="${escapeHtml(photo.name)}">${escapeHtml(photo.name)}</span>
            <span class="existing-flag" title="${photo.existingCity ? `Ville déjà présente dans le nom : ${escapeHtml(photo.existingCity)}${detected && fold(detected) !== fold(photo.existingCity) ? ` · Photon propose ${escapeHtml(detected)}` : ""}` : ""}" ${showExisting ? "" : "hidden"}>nom du fichier</span>
            <span class="modified-flag" title="Ville ou texte modifié">modifiée</span>
            <span class="renamed-flag" title="Photo renommée">renommée</span>
          </div>
          <div class="city-editor">
            <svg class="icon" aria-hidden="true"><use href="#i-pin"></use></svg>
            <input type="text" value="${escapeHtml(photo.city)}" placeholder="Saisir une ville"
              ${photo.renamed ? "readonly" : ""}
              aria-label="Ville de ${escapeHtml(photo.name)}"
              aria-readonly="${photo.renamed}"
              data-city-input autocomplete="off" spellcheck="false">
            <button class="city-reset" type="button" data-city-reset aria-label="Rétablir la ville d’origine" title="Rétablir la ville d’origine" ${photo.modified && !photo.renamed ? "" : "hidden"}>
              <svg class="icon" aria-hidden="true"><use href="#i-undo"></use></svg>
            </button>
          </div>
          <div class="photon-row" data-photon-row ${showSuggestion ? "" : "hidden"}>
            <button class="photon-suggestion" type="button" data-use-photon
              title="Le nom du fichier contient déjà une ville. Cliquez pour utiliser la ville détectée par Photon à la place.">
              <svg class="icon icon--xs" aria-hidden="true"><use href="#i-spark"></use></svg>
              Photon propose : <strong data-photon-city>${escapeHtml(detected)}</strong>
            </button>
          </div>
          <div class="existing-row" data-existing-row ${existingNote ? "" : "hidden"}>
            <span title="Le nom de ville est déjà présent dans le fichier. Photon confirme cette ville.">
              <svg class="icon icon--xs" aria-hidden="true"><use href="#i-check-circle"></use></svg>
              Ville déjà présente dans le nom
            </span>
          </div>
          <div class="text-editor">
            <svg class="icon" aria-hidden="true"><use href="#i-pencil"></use></svg>
            <input type="text" value="${escapeHtml(photo.userText)}" placeholder="+ Texte additionnel (optionnel)"
              ${photo.renamed ? "readonly" : ""}
              aria-label="Texte additionnel pour ${escapeHtml(photo.name)}"
              aria-readonly="${photo.renamed}"
              data-text-input autocomplete="off" spellcheck="false">
            <button class="text-reset" type="button" data-text-reset aria-label="Effacer le texte additionnel" title="Effacer le texte additionnel" ${photo.userText && !photo.renamed ? "" : "hidden"}>
              <svg class="icon icon--xs" aria-hidden="true"><use href="#i-x"></use></svg>
            </button>
          </div>
        </div>
      </article>
    `;
  }

  function renderGrid() {
    if (!els.photoGrid) return;
    const photos = visiblePhotos();
    els.photoGrid.innerHTML = photos.map((photo, index) => cardTemplate(photo, index)).join("");
    const empty = photos.length === 0;
    els.emptyState.hidden = !empty;
    els.photoGrid.hidden = empty;
    if (els.listFootnote) els.listFootnote.hidden = empty;
    updateSelectAllState();
    updateCounts();
  }

  function updateTabsUI() {
    const counts = {
      all: state.photos.length,
      located: locatedPhotos().length,
      missing: missingCount(),
      modified: state.photos.filter((photo) => photo.modified).length,
      renamed: state.photos.filter((photo) => photo.renamed).length,
    };
    els.tabs.forEach((tab) => {
      const filter = tab.dataset.filter;
      const countEl = $(`[data-count="${filter}"]`);
      if (countEl) countEl.textContent = String(counts[filter] ?? 0);
      const active = state.filter === filter;
      tab.classList.toggle("is-active", active);
      tab.setAttribute("aria-selected", String(active));
      tab.tabIndex = active ? 0 : -1;
    });
  }

  function updateCounts() {
    updateTabsUI();
    const actionable = selectedActionable();
    const renamed = renamedPhotos();
    const selected = state.photos.filter((photo) => photo.selected && normalizeCity(photo.city));
    const selectedCities = currentCityCount(selected);
    const totalActionable = actionable.length;
    const filterActive = state.filter !== "all" || state.cityFilter !== "all" || Boolean(state.search);

    if (els.renameButton) {
      const disabled = totalActionable === 0;
      els.renameButton.disabled = disabled;
      els.renameButton.setAttribute("aria-disabled", String(disabled));
      if (els.renameButtonLabel) {
        if (totalActionable === 0) {
          els.renameButtonLabel.textContent = renamed.length ? "Renommages effectués" : "Aucune photo à renommer";
        } else {
          els.renameButtonLabel.textContent = `Renommer ${totalActionable} ${plural(totalActionable, "photo")}`;
        }
      }
    }

    if (els.renameButton) {
      els.renameButton.title = filterActive
        ? "Renomme toutes les photos sélectionnées, y compris celles masquées par le filtre"
        : "Renommer les photos sélectionnées";
    }

    if (els.selectionNote) {
      const visibleSelected = visiblePhotos().filter((photo) => photo.selected && normalizeCity(photo.city)).length;
      if (state.renames.length) {
        els.selectionNote.textContent = `${state.renames.length} ${plural(state.renames.length, "photo")} renommée${state.renames.length > 1 ? "s" : ""} · annulation disponible`;
      } else if (filterActive) {
        els.selectionNote.textContent = `${selected.length} sélectionnée${selected.length > 1 ? "s" : ""} au total · ${visibleSelected} dans ce filtre`;
      } else {
        els.selectionNote.textContent = `${selected.length} ${plural(selected.length, "photo")} sélectionnée${selected.length > 1 ? "s" : ""} · ${selectedCities} ${plural(selectedCities, "commune")}`;
      }
    }

    if (els.mapPointCount) {
      const locatedVisible = visiblePhotos().filter((photo) => photo.hasGps).length;
      els.mapPointCount.textContent = `${locatedVisible} ${plural(locatedVisible, "point")}`;
    }
    if (els.mapCityCount) {
      const citiesVisible = currentCityCount(visiblePhotos().filter((photo) => photo.hasGps));
      els.mapCityCount.textContent = `${citiesVisible} ${plural(citiesVisible, "commune")}`;
    }
    const mapFooterStrong = $(".map-panel__footer strong");
    if (mapFooterStrong) mapFooterStrong.textContent = String(missingCount());

    if (els.existingSummary) {
      const existing = state.photos.filter((photo) => photo.existingCity).length;
      els.existingSummary.hidden = existing === 0;
      if (els.existingSummaryText) {
        els.existingSummaryText.textContent = existing > 0
          ? `${existing} ${plural(existing, "nom")} de ville ${existing > 1 ? "sont" : "est"} déjà présent${existing > 1 ? "s" : ""} dans ${existing > 1 ? "les fichiers" : "le fichier"}. Vérifiez ou remplacez ${existing > 1 ? "ces noms" : "ce nom"} si besoin.`
          : "";
      }
    }

    updateResumeBanner();

    if (els.selectAll) {
      const selectableVisible = visiblePhotos().filter((photo) =>
        normalizeCity(photo.city) && !photo.renamed && !isAlreadyNamed(photo)
      );
      els.selectAll.disabled = selectableVisible.length === 0;
      if (!selectableVisible.length) {
        els.selectAll.checked = false;
        els.selectAll.indeterminate = false;
      } else {
        updateSelectAllState();
      }
    }

    updateUndoBanner();
  }

  function updateResumeBanner() {
    if (!els.resumeBanner) return;
    const prior = state.priorRenames.length;
    els.resumeBanner.hidden = prior === 0;
    if (!prior) return;
    const remaining = state.photos.filter((photo) =>
      photo.selected && normalizeCity(photo.city) && !photo.renamed && !isAlreadyNamed(photo)
    ).length;
    if (els.resumeSummary) {
      els.resumeSummary.textContent = remaining > 0
        ? `${prior} ${plural(prior, "photo")} déjà renommée${prior > 1 ? "s" : ""} · ${remaining} encore à traiter.`
        : `${prior} ${plural(prior, "photo")} déjà renommée${prior > 1 ? "s" : ""} · tout est terminé.`;
    }
  }

  function updateSelectAllState() {
    if (!els.selectAll) return;
    const visible = visiblePhotos().filter((photo) =>
      normalizeCity(photo.city) && !photo.renamed && !isAlreadyNamed(photo)
    );
    if (!visible.length) {
      els.selectAll.checked = false;
      els.selectAll.indeterminate = false;
      return;
    }
    const selected = visible.filter((photo) => photo.selected).length;
    els.selectAll.checked = selected === visible.length;
    els.selectAll.indeterminate = selected > 0 && selected < visible.length;
  }

  function updateUndoBanner() {
    if (!els.undoBanner) return;
    const hasRenames = state.renames.length > 0;
    const resultsVisible = !els.views.results.hidden;
    els.undoBanner.hidden = !(hasRenames && resultsVisible);
    if (hasRenames) {
      const count = state.renames.length;
      if (els.undoBannerText) els.undoBannerText.textContent = `${count} ${plural(count, "photo")} renommée${count > 1 ? "s" : ""}.`;
    }
  }

  function applyCardState(photo) {
    const card = els.photoGrid?.querySelector(`[data-id="${photo.id}"]`);
    if (!card) return;
    const city = normalizeCity(photo.city);
    const detected = normalizeCity(photo.detectedCity);
    const alreadyNamed = isAlreadyNamed(photo);
    const locked = photo.renamed || alreadyNamed;
    card.classList.toggle("is-unselected", !photo.selected);
    card.classList.toggle("is-modified", photo.modified);
    card.classList.toggle("is-renamed", photo.renamed);
    card.classList.toggle("is-already-named", alreadyNamed);
    card.classList.toggle("has-existing-city", Boolean(photo.existingCity));
    card.classList.toggle("is-active", state.activeId === photo.id);
    const checkbox = $("[data-select]", card);
    if (checkbox) {
      checkbox.checked = photo.selected;
      checkbox.disabled = locked;
    }
    const checkLabel = $(".card-check", card);
    if (checkLabel) {
      checkLabel.title = photo.renamed
        ? "Photo déjà renommée dans une session précédente"
        : alreadyNamed
          ? "Nom déjà complet — modifiez la ville ou le texte pour le renommer"
          : "Inclure dans le renommage";
    }
    const input = $("[data-city-input]", card);
    if (input && document.activeElement !== input) input.value = photo.city;
    if (input) input.readOnly = photo.renamed;
    const reset = $("[data-city-reset]", card);
    if (reset) reset.hidden = !(photo.modified && !photo.renamed);
    const textInput = $("[data-text-input]", card);
    if (textInput && document.activeElement !== textInput) textInput.value = photo.userText;
    if (textInput) textInput.readOnly = photo.renamed;
    const textReset = $("[data-text-reset]", card);
    if (textReset) textReset.hidden = !(photo.userText && !photo.renamed);
    const showExisting = Boolean(photo.existingCity) && !photo.modified && !photo.renamed;
    const photonRow = $("[data-photon-row]", card);
    if (photonRow) photonRow.hidden = !(showExisting && fold(city) !== fold(detected));
    const photonCity = $("[data-photon-city]", card);
    if (photonCity) photonCity.textContent = detected;
    const existingRow = $("[data-existing-row]", card);
    if (existingRow) existingRow.hidden = !(showExisting && fold(city) === fold(detected));
    const existingFlag = $(".existing-flag", card);
    if (existingFlag) existingFlag.hidden = !showExisting;
  }

  function getCityInput(photo) {
    const card = els.photoGrid?.querySelector(`[data-id="${photo.id}"]`);
    return card ? $("[data-city-input]", card) : null;
  }

  function commitCityEdit(photo, { silent = false } = {}) {
    if (photo.renamed) return;
    photo.city = sanitizeCity(photo.city);
    photo.userText = sanitizeUserText(photo.userText);
    recalcPhoto(photo);
    applyCardState(photo);
    updateCounts();
    refreshMarker(photo);
    if (!matchCurrentFilters(photo)) {
      renderResults({ resetScroll: true });
    } else {
      renderCityChips();
      updateMapMarkers({ fit: false });
    }
    if (!silent) {
      const label = [normalizeCity(photo.city), normalizeCity(photo.userText)].filter(Boolean).join(" , ");
      announce(`${photo.name} : ${label || "aucune ville"}.`);
    }
  }

  /* ------------------------------------------------------------
     Carte
     ------------------------------------------------------------ */

  function markerSvg() {
    return `<svg viewBox="0 0 28 36" aria-hidden="true"><path d="M14 34.2S25.6 23.3 25.6 14A11.6 11.6 0 1 0 2.4 14c0 9.3 11.6 20.2 11.6 20.2Z" fill="currentColor" stroke="rgba(255,255,255,.92)" stroke-width="1.8"/><circle cx="14" cy="14" r="3.7" fill="rgba(255,255,255,.95)"/></svg>`;
  }

  function markerIcon(photo, active = false) {
    const classes = ["photo-marker"];
    if (active || state.activeId === photo.id) classes.push("is-active");
    if (!photo.selected) classes.push("is-off");
    return L.divIcon({
      className: classes.join(" "),
      html: markerSvg(),
      iconSize: [26, 34],
      iconAnchor: [13, 33],
      popupAnchor: [0, -30],
    });
  }

  function applyMarkerA11y(marker, photo) {
    const el = marker.getElement();
    if (!el) return;
    el.setAttribute("role", "button");
    el.setAttribute("tabindex", "0");
    const city = normalizeCity(photo.city) || (photo.hasGps ? "ville à confirmer" : "sans GPS");
    el.setAttribute("aria-label", `${photo.name}, ${city}. Cliquez pour retrouver la photo.`);
    el.setAttribute("title", `${photo.name} — ${normalizeCity(photo.city) || (photo.hasGps ? "Ville à confirmer" : "Sans GPS")}`);
  }

  function mapBasemapStyle() {
    return document.documentElement.dataset.theme === "dark"
      ? "https://tiles.openfreemap.org/styles/fiord"
      : "https://tiles.openfreemap.org/styles/positron";
  }

  function hideMapOffline() {
    els.mapOffline.hidden = true;
    state.mapOfflineShown = false;
    state.mapTileErrors = 0;
  }

  function showMapOffline() {
    if (state.mapOfflineShown) return;
    els.mapOffline.hidden = false;
    state.mapOfflineShown = true;
  }

  function attachBasemapEvents(layer) {
    layer._cfStyleLoaded = false;
    const started = performance.now();
    const watcher = window.setInterval(() => {
      if (state.mapTileLayer !== layer) {
        window.clearInterval(watcher);
        return;
      }
      const gl = layer?.getMaplibreMap?.();
      if (gl?.isStyleLoaded?.()) {
        layer._cfStyleLoaded = true;
        hideMapOffline();
        window.clearInterval(watcher);
        return;
      }
      if (performance.now() - started > 4200) {
        showMapOffline();
      }
    }, 300);
  }

  function createBasemapLayer() {
    if (!window.L?.maplibreGL) return null;
    const layer = L.maplibreGL({
      style: mapBasemapStyle(),
      attributionControl: true,
    });
    layer.addTo(state.map);
    attachBasemapEvents(layer);
    return layer;
  }

  function updateMapBasemap() {
    if (!state.map || !state.mapInitialized || !window.L?.maplibreGL) return;
    if (state.mapTileLayer) {
      state.map.removeLayer(state.mapTileLayer);
      state.mapTileLayer = null;
    }
    state.mapTileLayer = createBasemapLayer();
    // Le fond vectoriel vit dans le tilePane, sous les repères et les clusters.
    if (state.clusterGroup) state.clusterGroup.bringToFront?.();
  }

  function popupHtml(photo) {
    const city = normalizeCity(photo.city) || (photo.hasGps ? "Ville à confirmer" : "Sans GPS");
    const modified = photo.modified ? " · modifiée" : "";
    const country = photo.country ? ` · ${escapeHtml(photo.country)}` : "";
    return `
      <div class="map-popup">
        <img class="map-popup__image" src="${photo.thumb}" alt="">
        <div class="map-popup__body">
          <div class="map-popup__eyebrow"><svg class="icon" aria-hidden="true"><use href="#i-pin"></use></svg>${escapeHtml(city)}${country}${modified}</div>
          <strong class="map-popup__name" title="${escapeHtml(photo.name)}">${escapeHtml(photo.name)}</strong>
          <p class="map-popup__meta">${escapeHtml(formatDate(photo))} · ${escapeHtml(formatCoords(photo.lat, photo.lon))}</p>
          <div class="map-popup__actions">
            <button type="button" data-popup-details="${photo.id}"><svg class="icon" aria-hidden="true"><use href="#i-eye"></use></svg>Voir la fiche</button>
            <button type="button" data-popup-edit="${photo.id}"><svg class="icon" aria-hidden="true"><use href="#i-pencil"></use></svg>Modifier</button>
            <a href="${photo.hasGps ? osmUrl(photo.lat, photo.lon) : "#"}" target="_blank" rel="noopener noreferrer" aria-label="Ouvrir cette photo sur OpenStreetMap"><svg class="icon" aria-hidden="true"><use href="#i-external"></use></svg>OSM</a>
          </div>
        </div>
      </div>`;
  }

  function initMap(shouldFit = false) {
    if (state.mapInitialized) {
      if (shouldFit) fitMapToVisible();
      return true;
    }
    if (!window.L) {
      els.mapOffline.hidden = false;
      els.mapOffline.textContent = "Carte indisponible — la grille reste entièrement fonctionnelle.";
      return false;
    }
    if (!els.mapCanvas || els.mapCanvas.offsetWidth === 0) return false;
    try {
      state.map = L.map(els.mapCanvas, {
        zoomControl: false,
        attributionControl: true,
        minZoom: 4,
        maxZoom: 18,
        worldCopyJump: true,
        zoomSnap: 0.5,
        wheelPxPerZoomLevel: 120,
      });
      state.map.attributionControl.setPrefix(false);
      state.map.attributionControl.addAttribution(
        '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors · <a href="https://openfreemap.org/" target="_blank" rel="noopener noreferrer">OpenFreeMap</a> · <a href="https://www.openmaptiles.org/" target="_blank" rel="noopener noreferrer">OpenMapTiles</a>'
      );
      L.control.zoom({ position: "topleft" }).addTo(state.map);

      state.mapTileLayer = createBasemapLayer();

      const hasClusterPlugin = typeof L.markerClusterGroup === "function";
      state.hasClusterPlugin = hasClusterPlugin;
      state.clusterGroup = hasClusterPlugin
        ? L.markerClusterGroup({
          showCoverageOnHover: false,
          zoomToBoundsOnClick: true,
          spiderfyOnMaxZoom: true,
          removeOutsideVisibleBounds: true,
          animate: !reduceMotion,
          maxClusterRadius: 44,
          spiderfyDistanceMultiplier: 1.4,
          iconCreateFunction(cluster) {
            const count = cluster.getChildCount();
            return L.divIcon({
              className: "photo-cluster-icon",
              html: `<div class="photo-cluster" title="${count} photos — cliquez pour zoomer">${count}</div>`,
              iconSize: [42, 42],
              iconAnchor: [21, 21],
            });
          },
        })
        : L.layerGroup();
      state.clusterGroup.addTo(state.map);
      state.mapInitialized = true;
      state.map.setView([48.85, 1.15], 8);
      updateMapMarkers({ fit: shouldFit });
      window.setTimeout(() => state.map?.invalidateSize(), 80);
      return true;
    } catch (error) {
      console.error("Carte indisponible", error);
      els.mapOffline.hidden = false;
      els.mapOffline.textContent = "Carte indisponible — la grille reste entièrement fonctionnelle.";
      return false;
    }
  }

  function invalidateMap() {
    if (!state.map) return;
    window.setTimeout(() => state.map?.invalidateSize(), 30);
  }

  function updateMapMarkers({ fit = false } = {}) {
    if (!state.mapInitialized || !state.clusterGroup) return;
    const visible = visiblePhotos();
    const located = visible.filter((photo) => photo.hasGps && photo.lat !== null && photo.lon !== null);
    if (state.activeId && !located.some((photo) => photo.id === state.activeId)) {
      state.activeId = null;
      $$(".photo-card.is-active").forEach((card) => card.classList.remove("is-active"));
    }
    state.clusterGroup.clearLayers();
    state.markers.clear();

    located.forEach((photo) => {
      const active = state.activeId === photo.id;
      const marker = L.marker([photo.lat, photo.lon], {
        icon: markerIcon(photo, active),
        riseOnHover: true,
        keyboard: true,
        title: `${photo.name} — ${normalizeCity(photo.city) || (photo.hasGps ? "Ville à confirmer" : "Sans GPS")}`,
        autoPanOnFocus: true,
      });
      marker.bindPopup(popupHtml(photo), {
        maxWidth: 260,
        minWidth: 232,
        closeButton: true,
        autoPanPadding: [24, 24],
        className: "cf-popup",
      });
      marker.on("click", () => {
        activatePhoto(photo.id, "map");
      });
      marker.on("add", () => {
        applyMarkerA11y(marker, photo);
        if (state.activeId === photo.id) {
          marker.getElement()?.classList.add("is-active");
        }
      });
      state.markers.set(photo.id, marker);
      state.clusterGroup.addLayer(marker);
    });

    const empty = located.length === 0;
    els.mapEmpty.hidden = !empty;
    if (empty) {
      els.mapEmpty.querySelector("strong").textContent = "Aucune photo géolocalisée ici";
      els.mapEmpty.querySelector("p").textContent = state.filter === "missing"
        ? "Ce filtre ne contient que des photos sans coordonnées GPS."
        : "Essayez une autre recherche ou un autre filtre.";
    }

    if (els.mapPointCount) els.mapPointCount.textContent = `${located.length} ${plural(located.length, "point")}`;
    if (els.mapCityCount) {
      const cities = currentCityCount(located);
      els.mapCityCount.textContent = `${cities} ${plural(cities, "commune")}`;
    }
    if (fit) fitMapToBounds(located);
    window.requestAnimationFrame(() => {
      state.markers.forEach((marker, id) => {
        const photo = getPhoto(id);
        if (photo) applyMarkerA11y(marker, photo);
      });
    });
  }

  function fitMapToVisible() {
    const located = visiblePhotos().filter((photo) => photo.hasGps && photo.lat !== null && photo.lon !== null);
    fitMapToBounds(located);
  }

  function fitMapToBounds(photos, maxZoom = 12) {
    if (!state.map) return;
    if (!photos.length) {
      state.map.setView([48.85, 1.15], 8, { animate: !reduceMotion });
      return;
    }
    if (photos.length === 1) {
      state.map.setView([photos[0].lat, photos[0].lon], 14, { animate: !reduceMotion });
      return;
    }
    const bounds = L.latLngBounds(photos.map((photo) => [photo.lat, photo.lon]));
    state.map.fitBounds(bounds, {
      padding: [34, 34],
      maxZoom,
      animate: !reduceMotion,
    });
  }

  function fitMapToCity(city) {
    const photos = visiblePhotos().filter((photo) => photo.hasGps && fold(photo.city) === fold(city));
    if (!photos.length) return;
    if (!state.mapOpen) setMapOpen(true);
    window.setTimeout(() => {
      initMap(false);
      fitMapToBounds(photos, 13);
      invalidateMap();
    }, mobileMapQuery.matches ? 360 : 60);
  }

  function refreshMarker(photo) {
    if (!state.mapInitialized) return;
    const marker = state.markers.get(photo.id);
    if (!marker) return;
    marker.setIcon(markerIcon(photo, state.activeId === photo.id));
    marker.setPopupContent(popupHtml(photo));
    applyMarkerA11y(marker, photo);
  }

  function activatePhoto(id, source = "card") {
    const photo = getPhoto(id);
    if (!photo) return;
    const previous = state.activeId;
    state.activeId = id;
    $$(".photo-card.is-active").forEach((card) => card.classList.remove("is-active"));
    const card = els.photoGrid?.querySelector(`[data-id="${id}"]`);
    if (card) card.classList.add("is-active");

    if (state.mapInitialized) {
      if (previous && state.markers.has(previous)) {
        const previousPhoto = getPhoto(previous);
        if (previousPhoto) {
          const marker = state.markers.get(previous);
          marker.setIcon(markerIcon(previousPhoto, false));
        }
      }
      const marker = state.markers.get(id);
      if (marker) {
        marker.setIcon(markerIcon(photo, true));
        applyMarkerA11y(marker, photo);
      }
    }

    if (source === "map") {
      const city = normalizeCity(photo.city) || "sans GPS";
      announce(`${photo.name}, ${city}, mise en évidence dans la grille.`);
      window.setTimeout(() => scrollCardIntoView(id), 60);
    }
  }

  function focusPhotoOnMap(id, { openPopup = true } = {}) {
    const photo = getPhoto(id);
    if (!photo) return;
    if (!photo.hasGps || photo.lat === null || photo.lon === null) {
      openDetail(id);
      return;
    }
    if (!state.mapOpen) {
      setMapOpen(true);
    }

    activatePhoto(id, "card");
    const wait = (mobileMapQuery.matches && !state.mapInitialized) ? 360 : 50;
    window.setTimeout(() => {
      initMap(false);
      if (!state.mapInitialized) return;
      updateMapMarkers({ fit: false });
      const marker = state.markers.get(id);
      if (!marker) return;
      const open = () => {
        if (!openPopup) return;
        marker.openPopup();
        invalidateMap();
      };
      const visibleParent = state.hasClusterPlugin && state.clusterGroup?.getVisibleParent
        ? state.clusterGroup.getVisibleParent(marker)
        : null;
      if (visibleParent && visibleParent !== marker) {
        state.clusterGroup.zoomToShowLayer(marker, () => {
          open();
          invalidateMap();
        });
      } else {
        let opened = false;
        const onceOpen = () => {
          if (opened) return;
          opened = true;
          open();
        };
        state.map.flyTo([photo.lat, photo.lon], Math.max(state.map.getZoom(), 15), {
          duration: reduceMotion ? 0 : 0.85,
          easeLinearity: 0.25,
        });
        state.map.once("moveend", onceOpen);
        window.setTimeout(onceOpen, 980);
      }
    }, wait);
  }

  function scrollCardIntoView(id) {
    let card = els.photoGrid?.querySelector(`[data-id="${id}"]`);
    if (!card) {
      state.filter = "all";
      state.cityFilter = "all";
      state.search = "";
      if (els.searchInput) els.searchInput.value = "";
      renderResults();
      card = els.photoGrid?.querySelector(`[data-id="${id}"]`);
    }
    if (!card) return;
    card.classList.add("is-active");
    card.scrollIntoView({
      behavior: reduceMotion ? "auto" : "smooth",
      block: "center",
      inline: "nearest",
    });
  }

  /* ------------------------------------------------------------
     Détail photo
     ------------------------------------------------------------ */

  function openDetail(id, { focusCity = false } = {}) {
    const photo = getPhoto(id);
    if (!photo) return;
    state.detailId = id;
    state.detailDirty = false;
    state.detailCloseToMap = false;

    els.detailImage.src = photo.thumb;
    els.detailImage.alt = `Photo ${photo.name}`;
    els.detailStatusBadge.textContent = photo.hasGps ? "Géolocalisée" : "Sans GPS";
    els.detailStatusBadge.classList.toggle("is-missing", !photo.hasGps);
    els.detailEyebrow.textContent = photo.hasGps ? "Photo géolocalisée" : "Photo sans coordonnées";
    els.detailTitle.textContent = photo.name;
    const city = normalizeCity(photo.city);
    const detected = normalizeCity(photo.detectedCity);
    els.detailSub.textContent = `${city || "Ville à confirmer"}${photo.country ? ` · ${photo.country}` : ""}${photo.modified ? " · ville modifiée" : ""}`;
    els.detailCity.value = photo.city;
    els.detailCity.readOnly = photo.renamed;
    els.detailCity.setAttribute("aria-readonly", String(photo.renamed));
    els.detailCityReset.hidden = !photo.modified || photo.renamed;
    els.detailText.value = photo.userText;
    els.detailText.readOnly = photo.renamed;
    els.detailTextReset.hidden = !photo.userText || photo.renamed;
    const hasExisting = Boolean(photo.existingCity) && !photo.modified;
    const sameAsPhoton = fold(photo.city) === fold(detected);
    els.detailExisting.hidden = !hasExisting || photo.renamed;
    if (hasExisting) {
      els.detailPhotonCity.textContent = detected || "—";
      els.detailUsePhoton.hidden = sameAsPhoton || !detected;
      els.detailPhotonConfirm.hidden = !sameAsPhoton;
      els.detailPhotonConfirm.textContent = sameAsPhoton
        ? "La ville du fichier correspond à la ville détectée."
        : "";
    }
    els.detailCityHint.textContent = photo.renamed
      ? "Photo renommée. Annulez les renommages pour modifier la ville."
      : photo.modified
        ? "Ville ou texte modifié. Le nouveau nom est prévisualisé ci-dessous."
        : hasExisting
          ? "Le nom du fichier contient déjà une ville. Conservez-la ou utilisez la suggestion Photon."
          : "Ville détectée par Photon. Modifiez-la si une commune voisine vous semble plus juste.";
    els.detailCoords.textContent = formatCoords(photo.lat, photo.lon);
    els.detailDate.textContent = formatDate(photo);
    els.detailDevice.textContent = formatDevice(photo);
    els.detailDimensions.textContent = photo.width && photo.height ? `${photo.width} × ${photo.height}` : "—";
    els.detailRegion.textContent = formatRegion(photo);
    els.detailDetected.textContent = detected || "—";
    els.detailNewName.textContent = city ? proposedName(photo) : "—";
    els.detailMissingNote.hidden = photo.hasGps;
    els.detailSelected.checked = photo.selected;
    els.detailSelected.disabled = photo.renamed || isAlreadyNamed(photo);
    els.detailOsmLink.hidden = !photo.hasGps;
    els.detailMapButton.hidden = !photo.hasGps;
    if (photo.hasGps) {
      els.detailOsmLink.href = osmUrl(photo.lat, photo.lon);
    }
    if (!els.detailDialog.open) els.detailDialog.showModal();
    if (focusCity) {
      window.setTimeout(() => {
        els.detailCity.focus();
        els.detailCity.select();
      }, 60);
    }
  }

  function refreshDetailFields(photo) {
    const city = normalizeCity(photo.city);
    const detected = normalizeCity(photo.detectedCity);
    const alreadyNamed = isAlreadyNamed(photo);
    state.detailDirty = true;
    els.detailCityReset.hidden = !photo.modified || photo.renamed;
    els.detailTextReset.hidden = !photo.userText || photo.renamed;
    els.detailSelected.checked = photo.selected;
    els.detailSelected.disabled = photo.renamed || alreadyNamed;
    els.detailSub.textContent = `${city || "Ville à confirmer"}${photo.country ? ` · ${photo.country}` : ""}${photo.modified ? " · modifiée" : ""}`;
    els.detailNewName.textContent = city ? targetName(photo) : "—";
    const sameAsPhoton = fold(city) === fold(detected);
    const hasExisting = Boolean(photo.existingCity) && !photo.modified && !photo.renamed;
    els.detailExisting.hidden = !hasExisting;
    if (hasExisting) {
      els.detailUsePhoton.hidden = sameAsPhoton || !detected;
      els.detailPhotonConfirm.hidden = !sameAsPhoton;
      els.detailPhotonConfirm.textContent = sameAsPhoton ? "La ville du fichier correspond à la ville détectée." : "";
    }
    els.detailCityHint.textContent = photo.renamed
      ? "Photo renommée. Annulez les renommages pour modifier la ville."
      : photo.modified
        ? "Ville ou texte modifié. Le nouveau nom est prévisualisé ci-dessous."
        : photo.existingCity
          ? "Le nom du fichier contient déjà une ville. Conservez-la ou utilisez la suggestion Photon."
          : "Ville détectée par Photon. Modifiez-la si une commune voisine vous semble plus juste.";
    updateCounts();
    refreshMarker(photo);
  }

  function updateDetailFromCity() {
    const photo = getPhoto(state.detailId);
    if (!photo || photo.renamed) return;
    photo.city = els.detailCity.value;
    recalcPhoto(photo);
    refreshDetailFields(photo);
  }

  function updateDetailFromText() {
    const photo = getPhoto(state.detailId);
    if (!photo || photo.renamed) return;
    photo.userText = els.detailText.value;
    recalcPhoto(photo);
    refreshDetailFields(photo);
  }

  function closeDetail() {
    if (!els.detailDialog.open) return;
    els.detailDialog.close();
  }

  function afterDetailClosed() {
    const shouldRefresh = state.detailDirty;
    const focusMap = state.detailCloseToMap;
    const id = state.detailId;
    state.detailId = null;
    state.detailDirty = false;
    state.detailCloseToMap = false;
    if (shouldRefresh) {
      renderResults();
      invalidateMap();
    }
    if (focusMap && id) {
      window.setTimeout(() => focusPhotoOnMap(id), 80);
    }
  }

  /* ------------------------------------------------------------
     Renommage
     ------------------------------------------------------------ */

  function buildRenamePlan() {
    const items = [];
    const skipped = [];
    state.photos.forEach((photo) => {
      if (!photo.selected) return;
      const city = sanitizeCity(photo.city);
      if (!city) {
        skipped.push({ photo, reason: "sans-ville" });
        return;
      }
      if (photo.renamed) {
        skipped.push({ photo, reason: "deja-renommee" });
        return;
      }
      if (isAlreadyNamed(photo)) {
        skipped.push({ photo, reason: "deja-nommee" });
        return;
      }
      const target = proposedName(photo);
      if (fold(target) === fold(photo.name)) {
        skipped.push({ photo, reason: "deja-nommee" });
        return;
      }
      if (target.length > 250) {
        skipped.push({ photo, reason: "trop-long" });
        return;
      }
      items.push({
        photo,
        from: photo.name,
        to: target,
        city,
        text: sanitizeUserText(photo.userText),
      });
    });

    const targetCounts = new Map();
    items.forEach((item) => {
      const key = fold(item.to);
      targetCounts.set(key, (targetCounts.get(key) || 0) + 1);
    });
    const conflicts = items.filter((item) => (targetCounts.get(fold(item.to)) || 0) > 1);
    const existing = new Set(state.photos.map((photo) => fold(photo.name)));
    items.forEach((item) => {
      if (fold(item.from) !== fold(item.to) && existing.has(fold(item.to)) && (targetCounts.get(fold(item.to)) || 0) <= 1) {
        conflicts.push(item);
      }
    });

    return { items, skipped, conflicts };
  }

  function openRenameDialog() {
    if (!state.scanned) return;
    const plan = buildRenamePlan();
    if (!plan.items.length) {
      showToast("Aucune photo sélectionnée avec une ville.", "i-info");
      return;
    }
    const cities = new Set(plan.items.map((item) => fold(item.city))).size;
    const ignored = state.photos.length - plan.items.length;

    els.renameDialogCount.textContent = String(plan.items.length);
    els.renameSummaryFiles.textContent = String(plan.items.length);
    els.renameSummaryCities.textContent = String(cities);
    els.renameSummaryIgnored.textContent = String(ignored);
    els.renameIgnoredText.textContent = `${ignored} ${plural(ignored, "photo")} sans ville, déjà nommée${ignored > 1 ? "s" : ""} ou non sélectionnée${ignored > 1 ? "s" : ""} resteront intactes`;
    const exampleItems = plan.items.slice(0, 4);
    const textExample = plan.items.find((item) => item.text && !exampleItems.includes(item));
    if (textExample) exampleItems[exampleItems.length - 1] = textExample;
    els.renameExampleList.innerHTML = exampleItems.map((item) => `
      <div class="rename-example">
        <span class="old" title="${escapeHtml(item.from)}">${escapeHtml(item.from)}</span>
        <svg class="icon" aria-hidden="true"><use href="#i-arrow"></use></svg>
        <span class="new">${escapeHtml(item.to)}</span>
      </div>`).join("");

    const checks = $("#rename-checks");
    if (plan.conflicts.length) {
      checks.innerHTML = `
        <li class="is-warning"><svg class="icon icon--sm" aria-hidden="true"><use href="#i-alert"></use></svg> ${plan.conflicts.length} conflit${plan.conflicts.length > 1 ? "s" : ""} de nom à vérifier</li>
        <li><svg class="icon icon--sm" aria-hidden="true"><use href="#i-check-circle"></use></svg> Les autres fichiers peuvent être renommés</li>
        <li class="is-warning"><svg class="icon icon--sm" aria-hidden="true"><use href="#i-alert"></use></svg> ${ignored} photos ignorées</li>`;
    } else {
      checks.innerHTML = `
        <li><svg class="icon icon--sm" aria-hidden="true"><use href="#i-check-circle"></use></svg> Aucun conflit de nom détecté</li>
        <li><svg class="icon icon--sm" aria-hidden="true"><use href="#i-check-circle"></use></svg> Aucun nom ne dépasse la longueur autorisée</li>
        <li class="is-warning"><svg class="icon icon--sm" aria-hidden="true"><use href="#i-alert"></use></svg> ${ignored} photos sans ville, déjà nommées ou non sélectionnées resteront intactes</li>`;
    }
    els.renameDialogCard.classList.remove("is-renaming");
    els.renameProgress.hidden = true;
    els.renameProgressCount.textContent = `0 / ${plan.items.length}`;
    els.renameProgressBar.style.width = "0%";
    els.confirmRename.disabled = plan.conflicts.length > 0;
    els.confirmRenameLabel.textContent = plan.conflicts.length
      ? "Conflits à corriger"
      : `Renommer ${plan.items.length} ${plural(plan.items.length, "photo")}`;
    state.currentPlan = plan;
    if (!els.renameDialog.open) els.renameDialog.showModal();
    window.setTimeout(() => els.confirmRename.focus(), 50);
  }

  function executeRename({ instant = false } = {}) {
    const plan = state.currentPlan || buildRenamePlan();
    if (!plan.items.length) return;
    state.renaming = true;

    if (instant) {
      applyRenamePlan(plan);
      return;
    }

    els.renameDialogCard.classList.add("is-renaming");
    els.renameProgress.hidden = false;
    const total = plan.items.length;
    let done = 0;
    const duration = 1350;
    const start = performance.now();

    const tick = (now) => {
      const progress = Math.min(1, (now - start) / duration);
      done = Math.round(progress * total);
      els.renameProgressCount.textContent = `${done} / ${total}`;
      els.renameProgressBar.style.width = `${progress * 100}%`;
      if (progress < 1) {
        window.requestAnimationFrame(tick);
      } else {
        window.setTimeout(() => {
          applyRenamePlan(plan);
        }, 180);
      }
    };
    window.requestAnimationFrame(tick);
  }

  function applyRenamePlan(plan) {
    state.renames = plan.items.map((item) => ({
      id: item.photo.id,
      from: item.from,
      to: item.to,
      city: item.city,
      text: item.text || "",
    }));
    state.renames.forEach((entry) => state.sessionEntries.set(entry.id, entry));
    plan.items.forEach((item) => {
      const photo = item.photo;
      photo.name = item.to;
      photo.renamed = true;
      photo.renamedFrom = item.from;
      photo.renamedTo = item.to;
      photo.selected = false;
      photo.modified = false;
    });
    writeStoredSession();
    state.renaming = false;
    state.currentPlan = null;
    if (els.renameDialog.open) els.renameDialog.close();
    const groups = new Map();
    plan.items.forEach((item) => {
      const key = fold(item.city);
      if (!groups.has(key)) groups.set(key, { city: item.city, count: 0 });
      groups.get(key).count += 1;
    });
    const cities = Array.from(groups.values()).sort((a, b) => b.count - a.count || a.city.localeCompare(b.city, "fr"));
    els.successCount.textContent = String(plan.items.length);
    els.successCities.innerHTML = cities.map((group) => `<span>${escapeHtml(group.city)} <b>${group.count}</b></span>`).join("");
    const successExampleItems = plan.items.slice(0, 3);
    const successTextExample = plan.items.find((item) => item.text && !successExampleItems.includes(item));
    if (successTextExample) successExampleItems[successExampleItems.length - 1] = successTextExample;
    els.successExamples.innerHTML = successExampleItems.map((item) => `
      <div class="success-example">
        <span title="${escapeHtml(item.from)}">${escapeHtml(item.from)}</span>
        <svg class="icon" aria-hidden="true"><use href="#i-arrow"></use></svg>
        <strong>${escapeHtml(item.to)}</strong>
      </div>`).join("");
    renderResults({ resetScroll: true });
    showView("success");
    updateCounts();
    updateMapMarkers({ fit: false });
    announce(`${plan.items.length} photos renommées dans ${cities.length} communes.`);
    showToast(`${plan.items.length} photos renommées.`, "i-check-circle");
  }

  function undoRenames() {
    if (!state.renames.length) return;
    const count = state.renames.length;
    state.renames.forEach((entry) => {
      const photo = getPhoto(entry.id);
      if (!photo) return;
      photo.name = entry.from;
      photo.renamed = false;
      photo.renamedFrom = null;
      photo.renamedTo = null;
      photo.selected = Boolean(normalizeCity(photo.city)) && !isAlreadyNamed(photo);
      state.sessionEntries.delete(entry.id);
    });
    state.renames = [];
    writeStoredSession();
    if (!els.views.success.hidden) showView("results");
    renderResults({ resetScroll: true });
    updateUndoBanner();
    if (state.mapOpen) {
      initMap(false);
      invalidateMap();
    }
    showToast(`${count} ${plural(count, "renommage annulé", "renommages annulés")}.`, "i-undo");
    announce(`${count} renommages annulés.`);
  }

  function undoPriorRenames() {
    if (!state.priorRenames.length) return;
    const entries = state.priorRenames.slice();
    entries.forEach((entry) => {
      const photo = getPhoto(entry.id);
      if (!photo) return;
      photo.name = entry.from;
      photo.renamed = false;
      photo.renamedFrom = null;
      photo.renamedTo = null;
      photo.city = entry.city || photo.city;
      photo.userText = entry.text || "";
      state.sessionEntries.delete(entry.id);
      recalcPhoto(photo);
    });
    state.priorRenames = [];
    writeStoredSession();
    renderResults({ resetScroll: true });
    updateWelcomeResume();
    showToast(`${entries.length} ${plural(entries.length, "renommage annulé", "renommages annulés")}.`, "i-undo");
    announce(`${entries.length} renommages antérieurs annulés.`);
  }

  /* ------------------------------------------------------------
     Interactions
     ------------------------------------------------------------ */

  function setCityFilter(city) {
    state.cityFilter = city;
    if (city !== "all") {
      state.filter = "all";
    }
    renderResults({ resetScroll: true });
    if (state.mapOpen) {
      window.setTimeout(() => {
        initMap(false);
        if (city === "all") fitMapToVisible();
        else fitMapToCity(city);
      }, 60);
    }
  }

  function resetFilters() {
    state.filter = "all";
    state.cityFilter = "all";
    state.search = "";
    if (els.searchInput) els.searchInput.value = "";
    renderResults({ resetScroll: true });
    if (state.mapOpen) {
      initMap(false);
      fitMapToVisible();
    }
    announce("Filtres réinitialisés.");
  }

  function bindEvents() {
    els.themeToggle.addEventListener("click", () => {
      const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
      setTheme(next);
      announce(next === "dark" ? "Thème sombre activé." : "Thème clair activé.");
    });

    document.addEventListener("click", (event) => {
      const closeButton = event.target.closest("[data-close-dialog]");
      if (closeButton) {
        const dialog = closeButton.closest("dialog");
        if (dialog === els.renameDialog && state.renaming) return;
        closeDialog(dialog);
        return;
      }

      const actionEl = event.target.closest("[data-action]");
      if (!actionEl) return;
      const action = actionEl.dataset.action;
      if (!action) return;

      switch (action) {
        case "home":
          if (state.scanTimer) stopScan();
          showView("welcome");
          break;
        case "choose-folder":
          openDialog(els.folderDialog);
          break;
        case "start-scan":
          closeDialog(els.folderDialog);
          startScan();
          break;
        case "demo":
          closeDialog(els.folderDialog);
          startScan();
          break;
        case "resume-demo":
          resetPhotos();
          loadStoredSession();
          state.scanned = true;
          state.filter = "all";
          state.cityFilter = "all";
          state.search = "";
          if (els.searchInput) els.searchInput.value = "";
          enterResults({ fitMap: true });
          break;
        case "cancel-scan":
          cancelScan();
          break;
        case "skip-scan":
          stopScan();
          finishScan();
          break;
        case "about":
          openDialog(els.aboutDialog);
          break;
        case "reset-filters":
          resetFilters();
          break;
        case "map-fit":
          if (!state.mapOpen) setMapOpen(true);
          window.setTimeout(() => {
            initMap(false);
            fitMapToVisible();
            invalidateMap();
          }, mobileMapQuery.matches ? 340 : 60);
          break;
        case "map-close":
          setMapOpen(false);
          break;
        case "undo-rename":
          undoRenames();
          break;
        case "reset-session":
          resetDemoSession();
          break;
        case "undo-prior-renames":
          undoPriorRenames();
          break;
        case "back-results":
          showView("results");
          updateCounts();
          if (state.mapOpen) {
            initMap(false);
            invalidateMap();
          }
          break;
        case "open-folder":
          showToast("Disponible dans l’application Windows.", "i-folder");
          break;
        default:
          break;
      }
    });

    els.mapToggle.addEventListener("click", () => {
      setMapOpen(!state.mapOpen, { fit: !state.mapInitialized });
    });
    els.mapScrim.addEventListener("click", () => setMapOpen(false));

    $$(".modal").forEach((dialog) => {
      dialog.addEventListener("click", (event) => {
        if (event.target === dialog) {
          if (dialog === els.renameDialog && state.renaming) return;
          closeDialog(dialog);
        }
      });
    });
    els.renameDialog.addEventListener("cancel", (event) => {
      if (state.renaming) {
        event.preventDefault();
      }
    });
    els.renameDialog.addEventListener("close", () => {
      state.renaming = false;
      els.renameDialogCard.classList.remove("is-renaming");
      els.renameProgress.hidden = true;
    });
    els.detailDialog.addEventListener("close", afterDetailClosed);

    els.tabs.forEach((tab) => {
      tab.addEventListener("click", () => {
        state.filter = tab.dataset.filter;
        state.cityFilter = "all";
        renderResults({ resetScroll: true });
        if (state.mapOpen) invalidateMap();
        announce(`Filtre ${tab.textContent.trim()} appliqué.`);
      });
      tab.addEventListener("keydown", (event) => {
        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
        event.preventDefault();
        const index = els.tabs.indexOf(tab);
        let target = index;
        if (event.key === "ArrowLeft") target = (index - 1 + els.tabs.length) % els.tabs.length;
        if (event.key === "ArrowRight") target = (index + 1) % els.tabs.length;
        if (event.key === "Home") target = 0;
        if (event.key === "End") target = els.tabs.length - 1;
        els.tabs[target].focus();
        els.tabs[target].click();
      });
    });

    els.cityChips.addEventListener("click", (event) => {
      const chip = event.target.closest("[data-city]");
      if (!chip) return;
      const city = chip.dataset.city;
      setCityFilter(city === "all" || city === state.cityFilter ? "all" : city);
    });

    els.searchInput.addEventListener("input", () => {
      window.clearTimeout(state.searchTimer);
      state.searchTimer = window.setTimeout(() => {
        state.search = els.searchInput.value;
        renderResults({ resetScroll: true });
      }, 120);
    });
    els.searchInput.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        els.searchInput.value = "";
        state.search = "";
        renderResults({ resetScroll: true });
        els.searchInput.blur();
      }
    });
    els.sortSelect.addEventListener("change", () => {
      state.sort = els.sortSelect.value;
      renderGrid();
      if (els.photoScroll) els.photoScroll.scrollTop = 0;
    });

    els.selectAll.addEventListener("change", () => {
      const visible = visiblePhotos().filter((photo) =>
        normalizeCity(photo.city) && !photo.renamed && !isAlreadyNamed(photo)
      );
      visible.forEach((photo) => {
        photo.selected = els.selectAll.checked;
        applyCardState(photo);
        refreshMarker(photo);
      });
      updateCounts();
      announce(els.selectAll.checked
        ? `${visible.length} photos sélectionnées.`
        : `${visible.length} photos désélectionnées.`);
    });

    if (els.selectAll) {
      els.selectAll.title = "Sélectionne toutes les photos visibles ayant une ville";
    }

    els.renameButton.addEventListener("click", openRenameDialog);
    els.confirmRename.addEventListener("click", () => executeRename());
    els.photoGrid.addEventListener("click", (event) => {
      const card = event.target.closest(".photo-card");
      if (!card) return;
      const photo = getPhoto(card.dataset.id);
      if (!photo) return;
      const reset = event.target.closest("[data-city-reset]");
      if (reset) {
        event.preventDefault();
        photo.city = photo.originalCity || "";
        commitCityEdit(photo, { silent: true });
        const input = getCityInput(photo);
        if (input) input.value = photo.city;
        showToast(photo.originalCity ? "Ville d’origine rétablie." : "Ville effacée.", "i-undo");
        return;
      }
      const textReset = event.target.closest("[data-text-reset]");
      if (textReset) {
        event.preventDefault();
        photo.userText = photo.originalText || "";
        commitCityEdit(photo, { silent: true });
        const textInput = $(`[data-id="${photo.id}"] [data-text-input]`);
        if (textInput) textInput.value = photo.userText;
        showToast(photo.userText ? "Texte d’origine rétabli." : "Texte additionnel effacé.", "i-undo");
        return;
      }
      const usePhoton = event.target.closest("[data-use-photon]");
      if (usePhoton) {
        event.preventDefault();
        if (!photo.renamed && normalizeCity(photo.detectedCity)) {
          photo.city = photo.detectedCity;
          commitCityEdit(photo, { silent: true });
          const input = getCityInput(photo);
          if (input) input.value = photo.city;
          showToast(`Ville détectée appliquée : ${photo.detectedCity}.`, "i-spark");
        }
        return;
      }
      if (event.target.closest("[data-city-input]")) return;
      if (event.target.closest("[data-text-input]")) return;
      if (event.target.closest(".card-check")) return;
      if (event.target.closest("[data-card-expand]")) {
        openDetail(photo.id);
        return;
      }
      if (event.target.closest("[data-card-hit]")) {
        if (!photo.hasGps) openDetail(photo.id);
        else focusPhotoOnMap(photo.id);
        return;
      }
      if (!photo.hasGps) openDetail(photo.id);
      else focusPhotoOnMap(photo.id);
    });

    els.photoGrid.addEventListener("change", (event) => {
      if (!event.target.matches("[data-select]")) return;
      const card = event.target.closest(".photo-card");
      const photo = getPhoto(card?.dataset.id);
      if (!photo) return;
      photo.selected = event.target.checked;
      applyCardState(photo);
      refreshMarker(photo);
      updateCounts();
      announce(`${photo.name} ${photo.selected ? "sélectionnée" : "désélectionnée"}.`);
    });

    els.photoGrid.addEventListener("input", (event) => {
      const cityInput = event.target.closest("[data-city-input]");
      const textInput = event.target.closest("[data-text-input]");
      if (!cityInput && !textInput) return;
      const card = event.target.closest(".photo-card");
      const photo = getPhoto(card?.dataset.id);
      if (!photo || photo.renamed) return;
      if (cityInput) photo.city = cityInput.value;
      if (textInput) photo.userText = textInput.value;
      recalcPhoto(photo);
      applyCardState(photo);
      updateCounts();
      refreshMarker(photo);
    });

    els.photoGrid.addEventListener("mouseover", (event) => {
      const card = event.target.closest(".photo-card");
      if (!card || (event.relatedTarget && card.contains(event.relatedTarget))) return;
      const marker = state.markers.get(card.dataset.id);
      const element = marker?.getElement();
      if (element) element.classList.add("is-hover");
    });

    els.photoGrid.addEventListener("mouseout", (event) => {
      const card = event.target.closest(".photo-card");
      if (!card || (event.relatedTarget && card.contains(event.relatedTarget))) return;
      const marker = state.markers.get(card.dataset.id);
      const element = marker?.getElement();
      if (element) element.classList.remove("is-hover");
    });

    els.photoGrid.addEventListener("change", (event) => {
      const cityInput = event.target.closest("[data-city-input]");
      const textInput = event.target.closest("[data-text-input]");
      if (!cityInput && !textInput) return;
      const card = event.target.closest(".photo-card");
      const photo = getPhoto(card?.dataset.id);
      if (!photo || photo.renamed) return;
      if (cityInput) photo.city = sanitizeCity(photo.city);
      if (textInput) photo.userText = sanitizeUserText(photo.userText);
      commitCityEdit(photo, { silent: true });
      if (cityInput) cityInput.value = photo.city;
      if (textInput) textInput.value = photo.userText;
    });

    els.photoGrid.addEventListener("keydown", (event) => {
      const input = event.target.closest("[data-city-input], [data-text-input]");
      if (!input) return;
      const card = input.closest(".photo-card");
      const photo = getPhoto(card?.dataset.id);
      if (!photo) return;
      if (event.key === "Enter") {
        event.preventDefault();
        input.blur();
      }
      if (event.key === "Escape") {
        if (input.matches("[data-city-input]")) {
          photo.city = photo.originalCity || "";
          input.value = photo.city;
        } else {
          photo.userText = photo.originalText || "";
          input.value = photo.userText;
        }
        input.blur();
      }
    });

    els.mapCanvas.addEventListener("click", (event) => {
      const edit = event.target.closest("[data-popup-edit]");
      if (edit) {
        event.preventDefault();
        const id = edit.dataset.popupEdit;
        state.map?.closePopup();
        openDetail(id, { focusCity: true });
        return;
      }
      const details = event.target.closest("[data-popup-details]");
      if (details) {
        event.preventDefault();
        state.map?.closePopup();
        openDetail(details.dataset.popupDetails);
      }
    });

    els.detailCity.addEventListener("input", updateDetailFromCity);
    els.detailText.addEventListener("input", updateDetailFromText);
    [els.detailCity, els.detailText].forEach((input) => {
      input.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
          event.stopPropagation();
          closeDetail();
        }
        if (event.key === "Enter") {
          event.preventDefault();
          input.blur();
        }
      });
      input.addEventListener("change", () => {
        const photo = getPhoto(state.detailId);
        if (!photo || photo.renamed) return;
        photo.city = sanitizeCity(photo.city);
        photo.userText = sanitizeUserText(photo.userText);
        recalcPhoto(photo);
        els.detailCity.value = photo.city;
        els.detailText.value = photo.userText;
        refreshDetailFields(photo);
      });
    });
    els.detailCityReset.addEventListener("click", () => {
      const photo = getPhoto(state.detailId);
      if (!photo) return;
      photo.city = photo.originalCity || "";
      recalcPhoto(photo);
      els.detailCity.value = photo.city;
      refreshDetailFields(photo);
      showToast(photo.city ? "Ville d’origine rétablie." : "Ville effacée.", "i-undo");
    });
    els.detailTextReset.addEventListener("click", () => {
      const photo = getPhoto(state.detailId);
      if (!photo) return;
      photo.userText = photo.originalText || "";
      recalcPhoto(photo);
      els.detailText.value = photo.userText;
      refreshDetailFields(photo);
      showToast("Texte additionnel effacé.", "i-undo");
    });
    els.detailUsePhoton.addEventListener("click", () => {
      const photo = getPhoto(state.detailId);
      if (!photo || photo.renamed) return;
      photo.city = photo.detectedCity || "";
      recalcPhoto(photo);
      els.detailCity.value = photo.city;
      refreshDetailFields(photo);
      showToast(`Ville détectée appliquée : ${photo.city}.`, "i-spark");
    });
    els.detailSelected.addEventListener("change", () => {
      const photo = getPhoto(state.detailId);
      if (!photo) return;
      photo.selected = els.detailSelected.checked;
      state.detailDirty = true;
      updateCounts();
      refreshMarker(photo);
    });
    els.detailMapButton.addEventListener("click", () => {
      const id = state.detailId;
      if (!id) return;
      state.detailCloseToMap = true;
      closeDetail();
    });

    mobileMapQuery.addEventListener("change", () => {
      if (state.scanned) {
        state.mapOpen = !mobileMapQuery.matches;
        els.mapScrim.hidden = true;
        els.mapPanel.classList.remove("is-open");
        showView("results");
        updateMapToggle();
        invalidateMap();
      }
    });

    window.addEventListener("resize", () => {
      window.clearTimeout(state.resizeTimer);
      state.resizeTimer = window.setTimeout(() => {
        invalidateMap();
      }, 150);
    });

    document.addEventListener("keydown", (event) => {
      const target = event.target;
      const typing = target.matches("input, textarea, select, [contenteditable='true']");
      if (event.key === "/" && !typing && !els.views.results.hidden) {
        event.preventDefault();
        els.searchInput.focus();
      }
    });
  }

  function openDialog(dialog) {
    if (!dialog) return;
    if (!dialog.open) dialog.showModal();
  }

  function closeDialog(dialog) {
    if (!dialog || !dialog.open) return;
    if (dialog === els.renameDialog && state.renaming) return;
    dialog.close();
  }

  /* ------------------------------------------------------------
     Démarrage, liens profonds
     ------------------------------------------------------------ */

  function enterResults({ fitMap = true } = {}) {
    state.scanned = true;
    renderResults({ resetScroll: true });
    showView("results");
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        if (state.mapOpen) {
          initMap(fitMap);
          invalidateMap();
        }
      });
    });
  }

  function handleDeepLink() {
    const hash = window.location.hash.replace(/^#/, "");
    if (!hash) {
      showView("welcome");
      return;
    }
    if (hash === "analyse") {
      window.setTimeout(startScan, 250);
      return;
    }
    if (["resultats", "carte", "renommage", "succes", "detail", "reprise"].includes(hash)) {
      if (hash === "reprise" && !state.sessionEntries.size) {
        seedDemoSession();
      }
      if (hash === "carte") {
        state.mapOpen = true;
        updateMapToggle();
      }
      enterResults({ fitMap: true });
      if (hash === "carte") {
        window.setTimeout(() => {
          setMapOpen(true, { fit: true });
          const primary = DATA.primaryCity ||
            CITY_GROUPS.find((group) => state.photos.some((photo) => photo.hasGps && fold(photo.city) === fold(group.city)))?.city;
          if (primary) {
            initMap(false);
            fitMapToCity(primary);
          }
        }, 450);
      }
      if (hash === "renommage") {
        window.setTimeout(openRenameDialog, 420);
      }
      if (hash === "detail") {
        window.setTimeout(() => {
          const first = state.photos.find((photo) => photo.hasGps);
          if (first) openDetail(first.id);
        }, 420);
      }
      if (hash === "succes") {
        window.setTimeout(() => {
          state.currentPlan = buildRenamePlan();
          if (state.currentPlan?.items.length) executeRename({ instant: true });
        }, 200);
      }
      return;
    }
    showView("welcome");
  }

  function init() {
    initTheme();
    resetPhotos();
    loadStoredSession();
    setHeroPhotos();
    initStaticLabels();
    bindEvents();
    updateMapToggle();
    els.scanRing?.style.setProperty("--p", "0");
    updateTabsUI();
    updateCounts();
    updateWelcomeResume();
    handleDeepLink();
    // Préchargement des miniatures de l'accueil une fois le DOM prêt.
    $$('img[data-hero-photo]').forEach((img) => {
      if (img.src) img.fetchPriority = "high";
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
