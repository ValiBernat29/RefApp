<script setup>
import { ref, reactive, computed, onMounted } from 'vue';
import SlotPicker from './components/SlotPicker.vue';
import { fetchAssignment, saveAssignment } from './api.js';

const props = defineProps({ matchId: { type: Number, required: true } });

const SLOTS = [
  { key: 'main', field: 'mainRefereeId', label: 'Main Referee' },
  { key: 'assistant1', field: 'assistant1Id', label: 'Assistant 1' },
  { key: 'assistant2', field: 'assistant2Id', label: 'Assistant 2' },
];

const loading = ref(true);
const loadError = ref('');
const match = ref(null);
const candidates = ref({ main: [], assistant1: [], assistant2: [] });
const chosen = reactive({ main: null, assistant1: null, assistant2: null });

const saving = ref(false);
const saveError = ref('');
const confirmingUnavailable = ref(false);

/** For one slot: the other slots' referees, so the same person cannot be picked twice. */
function takenBy(slotKey) {
  const taken = {};
  for (const s of SLOTS) {
    if (s.key !== slotKey && chosen[s.key]) taken[chosen[s.key]] = s.label;
  }
  return taken;
}

const unavailableChosen = computed(() =>
  SLOTS.map((s) => candidates.value[s.key].find((o) => o.id === chosen[s.key]))
    .filter((o) => o?.isUnavailable)
    .map((o) => o.name),
);

function setChosen(key, id) {
  chosen[key] = id;
  confirmingUnavailable.value = false;
  saveError.value = '';
}

async function save(force = false) {
  if (!force && unavailableChosen.value.length) {
    confirmingUnavailable.value = true;
    return;
  }
  confirmingUnavailable.value = false;
  saving.value = true;
  saveError.value = '';
  const result = await saveAssignment(props.matchId, {
    mainRefereeId: chosen.main,
    assistant1Id: chosen.assistant1,
    assistant2Id: chosen.assistant2,
  });
  if (result.ok) {
    window.location.href = '/Matches';
    return;
  }
  saving.value = false;
  saveError.value = result.error;
}

onMounted(async () => {
  try {
    const data = await fetchAssignment(props.matchId);
    match.value = data.match;
    candidates.value = data.candidates;
    chosen.main = data.current.mainRefereeId;
    chosen.assistant1 = data.current.assistant1Id;
    chosen.assistant2 = data.current.assistant2Id;
  } catch (e) {
    loadError.value = e.message || 'Failed to load.';
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div v-if="loading" class="flex items-center gap-3 py-10 text-gray-500">
    <div class="h-5 w-5 animate-spin rounded-full border-2 border-gray-300 border-t-sky-500"></div>
    Loading referees...
  </div>

  <div v-else-if="loadError" class="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700" role="alert">
    {{ loadError }}
  </div>

  <div v-else class="space-y-6">
    <!-- Match banner -->
    <div class="flex items-center space-x-4 rounded-xl border-l-4 border-sky-400 bg-white p-5 card-shadow">
      <svg class="h-8 w-8 flex-shrink-0 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" />
      </svg>
      <div>
        <p class="font-semibold text-gray-800">{{ match.homeTeam }} vs {{ match.awayTeam }}</p>
        <p class="text-sm text-gray-500">{{ match.date }} &middot; {{ match.location }}</p>
      </div>
    </div>

    <!-- Slot pickers -->
    <div class="grid gap-6 lg:grid-cols-3">
      <SlotPicker
        v-for="s in SLOTS"
        :key="s.key"
        :label="s.label"
        :options="candidates[s.key]"
        :model-value="chosen[s.key]"
        :taken-by="takenBy(s.key)"
        @update:model-value="setChosen(s.key, $event)"
      />
    </div>

    <!-- Actions -->
    <div class="space-y-3">
      <div
        v-if="confirmingUnavailable"
        class="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-800"
        role="alertdialog"
      >
        <p class="font-semibold">Unavailable on this date: {{ unavailableChosen.join(', ') }}</p>
        <p class="mt-1">These referees marked themselves unavailable. Assign them anyway?</p>
        <div class="mt-3 flex gap-2">
          <button type="button" class="rounded-lg bg-amber-500 px-4 py-2 font-medium text-white hover:bg-amber-600" @click="save(true)">
            Assign anyway
          </button>
          <button type="button" class="rounded-lg border border-amber-300 px-4 py-2 font-medium hover:bg-amber-100" @click="confirmingUnavailable = false">
            Go back
          </button>
        </div>
      </div>

      <p v-if="saveError" class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
        {{ saveError }}
      </p>

      <div class="flex items-center gap-3">
        <button
          type="button"
          class="rounded-lg bg-[#6eb7e4] px-6 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#5ca5d2] disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="saving"
          @click="save()"
        >
          {{ saving ? 'Saving...' : 'Save Assignments' }}
        </button>
        <a
          href="/Matches"
          class="rounded-lg border border-[#91b5bf] px-6 py-2.5 text-sm font-medium text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]"
        >
          Back to Matches
        </a>
      </div>
    </div>
  </div>
</template>
