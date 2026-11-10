<script setup>
import { ref, shallowRef, reactive, computed, watch, nextTick, onMounted } from 'vue';
import MapCanvas from '../components/MapCanvas.vue';
import SlotCard from '../components/SlotCard.vue';
import RefereeCard from '../components/RefereeCard.vue';
import { fetchRouteMapData } from '../api/routeMap.js';
import { saveAssignment } from '../api/assignments.js';
import { ARAD_CENTER, geocodeCity, fetchRoute, computeCoverage } from '../mapGeometry.js';

const SLOTS = [
  { key: 'main', label: 'Main Referee', short: 'Main', kind: 'main' },
  { key: 'asst1', label: 'Assistant 1', short: 'Asst1', kind: 'asst' },
  { key: 'asst2', label: 'Assistant 2', short: 'Asst2', kind: 'asst' },
];
const slotLabel = (key) => SLOTS.find((s) => s.key === key)?.label ?? '';

// ── State ──────────────────────────────────────────────────────────────────
const referees = ref([]);
const matches = ref([]);
const loading = ref(true);
const loadError = ref('');

const selectedMatchId = ref('');
const slots = reactive({ main: null, asst1: null, asst2: null }); // key -> referee id
const activeSlot = ref(null);

const originCity = ref('Arad');
const bufferKm = ref(10);
const route = shallowRef(null); // { geometry, distanceKm, durationMin }
const routing = ref(false);
const routeError = ref('');

const saving = ref(false);
const message = reactive({ text: '', kind: '' });

const mapRef = ref(null);
const listEl = ref(null);
const highlightedId = ref('');

// ── Derived ────────────────────────────────────────────────────────────────
const selectedMatch = computed(() => matches.value.find((m) => m.id === selectedMatchId.value) ?? null);
const refereeById = computed(() => new Map(referees.value.map((r) => [r.id, r])));

const coverage = computed(() =>
  route.value ? computeCoverage(route.value.geometry, referees.value, bufferKm.value) : null,
);
const onRouteIds = computed(() => coverage.value?.onRouteIds ?? new Set());
const assignedIds = computed(() => new Set(Object.values(slots).filter(Boolean)));

const slotOf = (id) => SLOTS.find((s) => slots[s.key] === id)?.short ?? '';

const sortedReferees = computed(() =>
  [...referees.value].sort((a, b) => {
    const aAss = assignedIds.value.has(a.id);
    const bAss = assignedIds.value.has(b.id);
    if (aAss !== bAss) return aAss ? -1 : 1;
    const aOn = onRouteIds.value.has(a.id);
    const bOn = onRouteIds.value.has(b.id);
    if (aOn !== bOn) return aOn ? -1 : 1;
    return (a.name || '').localeCompare(b.name || '');
  }),
);

const refCountText = computed(() =>
  coverage.value ? `(${onRouteIds.value.size} on route)` : `(${referees.value.length})`,
);

// ── Messages ───────────────────────────────────────────────────────────────
function setMessage(text = '', kind = '') {
  message.text = text;
  message.kind = kind;
}

// ── Slots ──────────────────────────────────────────────────────────────────
function toggleSlot(key) {
  activeSlot.value = activeSlot.value === key ? null : key;
}

function clearSlot(key) {
  slots[key] = null;
}

function resetSlots() {
  for (const s of SLOTS) slots[s.key] = null;
  activeSlot.value = null;
  setMessage();
}

function pickReferee(id) {
  const key = activeSlot.value;
  if (!key || !selectedMatch.value) return;
  const ref_ = refereeById.value.get(id);
  if (!ref_) return;

  const clash = SLOTS.find((s) => s.key !== key && slots[s.key] === id);
  if (clash) {
    setMessage(`${ref_.name} is already in ${clash.label}`, 'err');
    return;
  }
  slots[key] = id;
  activeSlot.value = null;
  setMessage();
}

function selectCard(r) {
  if (activeSlot.value) {
    pickReferee(r.id);
    return;
  }
  mapRef.value?.focusReferee(r.id);
  highlightedId.value = r.id;
  nextTick(() => {
    listEl.value?.querySelector(`[data-id="${CSS.escape(r.id)}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  });
}

// ── Routing ────────────────────────────────────────────────────────────────
let routeSeq = 0; // guards against out-of-order responses when switching matches quickly

async function computeRoute() {
  const m = selectedMatch.value;
  if (!m || m.lat == null || m.lon == null) return;

  const seq = ++routeSeq;
  routing.value = true;
  routeError.value = '';
  try {
    let origin = ARAD_CENTER;
    const city = originCity.value.trim() || 'Arad';
    if (city.toLowerCase() !== 'arad') {
      const found = await geocodeCity(city);
      if (found) origin = found;
      else routeError.value = `Could not find "${city}". Using Arad.`;
    }
    const result = await fetchRoute(origin, [m.lat, m.lon]);
    if (seq !== routeSeq) return;
    route.value = result;
    if (!result) routeError.value = 'No route found.';
  } catch {
    if (seq === routeSeq) routeError.value = 'Routing service unavailable.';
  } finally {
    if (seq === routeSeq) routing.value = false;
  }
}

watch(selectedMatchId, () => {
  routeSeq++; // invalidate any in-flight request
  route.value = null;
  routeError.value = '';
  highlightedId.value = '';
  resetSlots();

  const m = selectedMatch.value;
  if (!m) return;
  const existing = { main: m.mainRefereeId, asst1: m.assistant1Id, asst2: m.assistant2Id };
  for (const s of SLOTS) {
    if (existing[s.key] && refereeById.value.has(existing[s.key])) slots[s.key] = existing[s.key];
  }
  computeRoute();
});

// ── Save ───────────────────────────────────────────────────────────────────
async function save() {
  const m = selectedMatch.value;
  if (!m) return;
  saving.value = true;
  const payload = {
    mainRefereeId: slots.main,
    assistant1Id: slots.asst1,
    assistant2Id: slots.asst2,
  };
  try {
    await saveAssignment(m.id, payload);
    setMessage('Saved!', 'ok');
    m.mainRefereeId = payload.mainRefereeId;
    m.assistant1Id = payload.assistant1Id;
    m.assistant2Id = payload.assistant2Id;
  } catch (e) {
    setMessage(e.message, 'err');
  } finally {
    saving.value = false;
  }
}

// ── Load ───────────────────────────────────────────────────────────────────
onMounted(async () => {
  try {
    const data = await fetchRouteMapData();
    referees.value = data.referees;
    matches.value = data.matches;
  } catch (e) {
    loadError.value = e.message || 'Failed to load data.';
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="flex h-screen overflow-hidden bg-slate-900 font-sans">
    <!-- Side panel -->
    <aside class="flex w-[370px] shrink-0 flex-col overflow-hidden border-r border-slate-700 bg-slate-800">
      <header class="bg-gradient-to-br from-sky-500 to-indigo-500 px-[18px] py-3.5">
        <RouterLink to="/dashboard" class="text-xs text-white/80 hover:text-white">&larr; Dashboard</RouterLink>
        <h1 class="text-[15px] font-bold text-white">Route Map</h1>
        <span class="text-[11px] text-white/70">Arad County &middot; Referee Locations</span>
      </header>

      <section class="border-b border-slate-700 px-4 py-3">
        <label for="match-select" class="mb-1.5 block text-[10px] font-semibold uppercase tracking-wider text-slate-500">Select Match</label>
        <select
          id="match-select"
          v-model="selectedMatchId"
          class="w-full rounded-lg border-slate-700 bg-slate-900 px-2.5 py-2 text-[13px] text-slate-200 focus:border-sky-500 focus:ring-sky-500"
        >
          <option value="">&mdash; pick a match &mdash;</option>
          <option v-for="m in matches" :key="m.id" :value="m.id">
            {{ m.date }} &middot; {{ m.homeTeam }} vs {{ m.awayTeam }}{{ m.lat == null ? ' (no coords)' : '' }}
          </option>
        </select>
      </section>

      <section v-if="selectedMatch" class="border-b border-slate-700 px-4 py-3">
        <div class="mb-2 text-[10px] font-bold uppercase tracking-wider text-slate-500">Assign Referees</div>
        <SlotCard
          v-for="s in SLOTS"
          :key="s.key"
          :label="s.label"
          :kind="s.kind"
          :referee="refereeById.get(slots[s.key]) ?? null"
          :active="activeSlot === s.key"
          @activate="toggleSlot(s.key)"
          @clear="clearSlot(s.key)"
        />
        <p v-if="activeSlot" class="mt-1.5 animate-pulse text-center text-[11px] text-sky-500">
          &darr; Click a referee below or on the map
        </p>
        <button
          type="button"
          class="mt-2.5 w-full rounded-[9px] bg-gradient-to-br from-emerald-500 to-sky-500 py-2.5 text-[13px] font-bold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
          :disabled="saving"
          @click="save"
        >
          {{ saving ? 'Saving...' : 'Save Assignments' }}
        </button>
        <div
          class="mt-1.5 min-h-4 text-center text-xs"
          :class="message.kind === 'ok' ? 'text-emerald-400' : 'text-red-400'"
          role="status"
        >
          {{ message.text }}
        </div>
      </section>

      <section class="border-b border-slate-700 px-4 py-3">
        <label for="origin-city" class="mb-1.5 block text-[10px] font-semibold uppercase tracking-wider text-slate-500">Route From</label>
        <form class="flex items-center gap-2" @submit.prevent="computeRoute">
          <input
            id="origin-city"
            v-model="originCity"
            type="text"
            placeholder="Origin city..."
            class="min-w-0 flex-1 rounded-lg border-slate-700 bg-slate-900 px-2.5 py-2 text-[13px] text-slate-200 focus:border-sky-500 focus:ring-sky-500"
          />
          <button
            type="submit"
            class="whitespace-nowrap rounded-lg bg-gradient-to-br from-sky-500 to-indigo-500 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50"
            :disabled="routing || !selectedMatch"
          >
            {{ routing ? '...' : 'Show Route' }}
          </button>
        </form>
        <p v-if="routeError" class="mt-1.5 text-xs text-amber-400">{{ routeError }}</p>
        <div class="mt-2 flex items-center gap-2">
          <label for="buffer-km" class="whitespace-nowrap text-[11px] text-slate-400">Buffer &plusmn;</label>
          <input id="buffer-km" v-model.number="bufferKm" type="range" min="2" max="30" step="1" class="flex-1 accent-sky-500" />
          <span class="min-w-[38px] text-right text-xs font-semibold text-sky-500">{{ bufferKm }} km</span>
        </div>
      </section>

      <section v-if="route" class="flex gap-4 border-b border-slate-800 bg-slate-900 px-4 py-2">
        <div>
          <span class="block text-[10px] uppercase tracking-wide text-slate-500">Distance</span>
          <span class="text-sm font-bold text-slate-200">{{ route.distanceKm.toFixed(1) }} km</span>
        </div>
        <div>
          <span class="block text-[10px] uppercase tracking-wide text-slate-500">Duration</span>
          <span class="text-sm font-bold text-slate-200">{{ route.durationMin }} min</span>
        </div>
        <div>
          <span class="block text-[10px] uppercase tracking-wide text-slate-500">On Route</span>
          <span class="text-sm font-bold text-slate-200">{{ onRouteIds.size }} ref{{ onRouteIds.size !== 1 ? 's' : '' }}</span>
        </div>
      </section>

      <div class="px-4 pb-1 pt-2 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
        Referees <span class="text-sky-500">{{ refCountText }}</span>
      </div>
      <div ref="listEl" class="thin-scroll flex-1 overflow-y-auto px-2.5 py-1.5">
        <RefereeCard
          v-for="r in sortedReferees"
          :key="r.id"
          :referee="r"
          :on-route="onRouteIds.has(r.id)"
          :off-route-km="coverage?.offRouteKm[r.id] ?? null"
          :assigned-slot="slotOf(r.id)"
          :slot-active="!!activeSlot"
          :highlighted="highlightedId === r.id"
          @select="selectCard(r)"
          @assign="pickReferee(r.id)"
        />
      </div>

      <footer class="flex flex-wrap gap-3 border-t border-slate-700 px-4 py-2.5 text-[10px] text-slate-400">
        <span class="flex items-center gap-[5px]"><i class="h-[9px] w-[9px] rounded-full bg-blue-500"></i> Referee</span>
        <span class="flex items-center gap-[5px]"><i class="h-[9px] w-[9px] rounded-full bg-emerald-500"></i> On route</span>
        <span class="flex items-center gap-[5px]"><i class="h-[9px] w-[9px] rounded-full bg-indigo-500"></i> Assigned</span>
        <span class="flex items-center gap-[5px]"><i class="h-[9px] w-[9px] rounded-full bg-orange-500"></i> Match venue</span>
      </footer>
    </aside>

    <!-- Map -->
    <main class="relative flex-1 bg-slate-900">
      <MapCanvas
        ref="mapRef"
        :referees="referees"
        :match="selectedMatch"
        :route-geometry="route?.geometry ?? null"
        :buffer-zone="coverage?.buffer ?? null"
        :on-route-ids="onRouteIds"
        :assigned-ids="assignedIds"
        :active-slot-label="activeSlot ? slotLabel(activeSlot) : null"
        @pick-referee="pickReferee"
      />

      <div v-if="loading" class="absolute inset-0 z-[9999] flex flex-col items-center justify-center gap-3 bg-slate-900/85">
        <div class="h-9 w-9 animate-spin rounded-full border-[3px] border-slate-700 border-t-sky-500"></div>
        <p class="text-sm text-slate-400">Loading...</p>
      </div>

      <div v-else-if="loadError" class="absolute inset-0 z-[9999] flex items-center justify-center bg-slate-900/85">
        <p class="rounded-xl border border-red-500/40 bg-slate-800 px-6 py-4 text-sm text-red-400">{{ loadError }}</p>
      </div>

      <div
        v-else-if="!selectedMatch"
        class="pointer-events-none absolute left-1/2 top-1/2 z-[1000] -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-slate-700 bg-slate-800/90 px-8 py-6 text-center backdrop-blur"
      >
        <div class="mb-2 text-4xl">&#x1F5FA;</div>
        <strong class="text-slate-200">Select a match on the left</strong>
        <p class="mt-1.5 text-[13px] text-slate-400">
          The road route and buffer zone will appear here.<br />Referees within the buffer are highlighted as "on the way".
        </p>
      </div>
    </main>
  </div>
</template>
