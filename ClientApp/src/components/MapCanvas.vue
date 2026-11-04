<script setup>
import { ref, onMounted, onBeforeUnmount, watch } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { ARAD_CENTER } from '../routing.js';

const props = defineProps({
  referees: { type: Array, required: true },
  match: { type: Object, default: null },
  routeGeometry: { type: Object, default: null },
  bufferZone: { type: Object, default: null },
  onRouteIds: { type: Set, required: true },
  assignedIds: { type: Set, required: true },
  /** Label of the slot currently being filled, or null when no slot is active. */
  activeSlotLabel: { type: String, default: null },
});

const emit = defineEmits(['pick-referee']);

const mapEl = ref(null);

const COLORS = { default: '#3b82f6', onRoute: '#10b981', assigned: '#6366f1' };

// Leaflet objects are kept out of Vue's reactivity on purpose.
let map = null;
let markerLayer = null;
let routeLayer = null;
let bufferLayer = null;
let matchMarker = null;
const markers = new Map(); // refereeId -> { marker, color }
let lastRouteGeometry = null;
let resizeObserver = null;

const roleText = (role) =>
  role === 'MainReferee' ? 'Main Referee' : role === 'AssistantReferee' ? 'Assistant Referee' : 'No preference';

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text != null) node.textContent = text;
  return node;
}

function row(label, value) {
  const r = el('div', 'pp-row', `${label}: `);
  r.appendChild(el('b', null, value));
  return r;
}

function refereeIcon(color, letter) {
  const badge = el(
    'div',
    'flex h-8 w-8 items-center justify-center rounded-full border-2 border-white/60 text-[11px] font-bold text-white shadow-lg',
    letter,
  );
  badge.style.background = color;
  return L.divIcon({ className: '', html: badge, iconSize: [32, 32], iconAnchor: [16, 16], popupAnchor: [0, -18] });
}

function matchIcon() {
  const badge = el(
    'div',
    'flex h-10 w-10 items-center justify-center rounded-full border-2 border-white bg-gradient-to-br from-orange-500 to-red-500 text-[15px] font-bold text-white shadow-xl',
    'M',
  );
  return L.divIcon({ className: '', html: badge, iconSize: [40, 40], iconAnchor: [20, 40], popupAnchor: [0, -42] });
}

function colorFor(id) {
  if (props.assignedIds.has(id)) return COLORS.assigned;
  if (props.onRouteIds.has(id)) return COLORS.onRoute;
  return COLORS.default;
}

function buildRefereePopup(r) {
  const box = el('div');
  box.appendChild(el('div', 'pp-title', r.name));
  box.appendChild(row('City', r.city));
  box.appendChild(row('Role', roleText(r.preferredRole)));
  if (r.hasCar) box.appendChild(el('div', 'pp-row', 'Has a car'));
  return box;
}

function syncMarkers() {
  const seen = new Set();
  for (const r of props.referees) {
    if (r.lat == null || r.lon == null) continue;
    seen.add(r.id);
    const color = colorFor(r.id);
    const existing = markers.get(r.id);
    if (!existing) {
      const marker = L.marker([r.lat, r.lon], { icon: refereeIcon(color, (r.name || '?')[0].toUpperCase()) });
      marker.bindPopup(() => buildRefereePopup(r));
      marker.on('click', () => {
        // With a slot active, a click assigns directly instead of showing the info popup.
        if (props.activeSlotLabel) {
          marker.closePopup();
          emit('pick-referee', r.id);
        }
      });
      marker.addTo(markerLayer);
      markers.set(r.id, { marker, color });
    } else if (existing.color !== color) {
      existing.marker.setIcon(refereeIcon(color, (r.name || '?')[0].toUpperCase()));
      existing.color = color;
    }
  }
  for (const [id, entry] of markers) {
    if (!seen.has(id)) {
      markerLayer.removeLayer(entry.marker);
      markers.delete(id);
    }
  }
}

function syncMatchMarker() {
  if (matchMarker) {
    map.removeLayer(matchMarker);
    matchMarker = null;
  }
  const m = props.match;
  if (!m || m.lat == null || m.lon == null) return;
  const box = el('div');
  box.appendChild(el('div', 'pp-title', `${m.homeTeam} vs ${m.awayTeam}`));
  box.appendChild(el('div', 'pp-row', m.date));
  box.appendChild(el('div', 'pp-row', m.city || 'Unknown city'));
  matchMarker = L.marker([m.lat, m.lon], { icon: matchIcon() }).bindPopup(box).addTo(map);
}

function syncRoute() {
  if (routeLayer) map.removeLayer(routeLayer);
  if (bufferLayer) map.removeLayer(bufferLayer);
  routeLayer = bufferLayer = null;

  if (props.routeGeometry) {
    routeLayer = L.geoJSON(props.routeGeometry, { style: { color: '#0ea5e9', weight: 5, opacity: 0.85 } }).addTo(map);
  }
  if (props.bufferZone) {
    bufferLayer = L.geoJSON(props.bufferZone, {
      style: { fillColor: '#0ea5e9', fillOpacity: 0.09, color: '#0ea5e9', weight: 1.5, opacity: 0.4, dashArray: '6 4' },
    }).addTo(map);
    // Re-frame only when a new route arrives, not while dragging the buffer slider.
    if (props.routeGeometry !== lastRouteGeometry) {
      map.fitBounds(bufferLayer.getBounds(), { padding: [40, 40] });
    }
  }
  lastRouteGeometry = props.routeGeometry;
}

/** Fly to a referee and open their popup. Called from the parent via a template ref. */
function focusReferee(id) {
  const entry = markers.get(id);
  if (!entry) return;
  map.flyTo(entry.marker.getLatLng(), 13, { duration: 0.8 });
  entry.marker.openPopup();
}

defineExpose({ focusReferee });

onMounted(() => {
  map = L.map(mapEl.value, { zoomControl: false }).setView(ARAD_CENTER, 10);
  L.control.zoom({ position: 'topright' }).addTo(map);

  // Keyless dark basemap (Esri Dark Gray Canvas): roads + place labels only.
  L.tileLayer(
    'https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{z}/{y}/{x}',
    { attribution: 'Tiles &copy; Esri', maxZoom: 16 },
  ).addTo(map);
  L.tileLayer(
    'https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Reference/MapServer/tile/{z}/{y}/{x}',
    { attribution: '', maxZoom: 16, pane: 'overlayPane' },
  ).addTo(map);

  L.circleMarker(ARAD_CENTER, { radius: 8, fillColor: '#f59e0b', color: '#fff', weight: 2, fillOpacity: 0.9 })
    .addTo(map)
    .bindPopup(() => {
      const box = el('div');
      box.appendChild(el('div', 'pp-title', 'Arad'));
      box.appendChild(el('div', 'pp-row', 'Route origin'));
      return box;
    });

  markerLayer = L.layerGroup().addTo(map);
  syncMarkers();
  syncMatchMarker();
  syncRoute();

  // Leaflet measures its container once; re-measure whenever the layout changes size.
  resizeObserver = new ResizeObserver(() => map?.invalidateSize());
  resizeObserver.observe(mapEl.value);
});

onBeforeUnmount(() => {
  resizeObserver?.disconnect();
  map?.remove();
  map = null;
  markers.clear();
});

watch(() => [props.referees, props.assignedIds, props.onRouteIds], () => map && syncMarkers());
watch(() => props.match, () => map && syncMatchMarker());
watch(() => [props.routeGeometry, props.bufferZone], () => map && syncRoute());
</script>

<template>
  <div ref="mapEl" class="h-full w-full bg-slate-900"></div>
</template>
