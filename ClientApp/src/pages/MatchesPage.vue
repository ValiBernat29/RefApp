<script setup>
import { ref, reactive, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import { listMatches } from '../api/matches.js';
import { getLookups } from '../api/lookups.js';
import { formatDateTime } from '../format.js';
import { btnPrimary, btnPrimarySm, btnOutline, btnOutlineSm, card, inputClass, tableHead, tableRow } from '../components/ui/classes.js';

const leagues = ref([]);
const matches = ref([]);
const loading = ref(true);
const error = ref('');

// The filter in the form; `applied` is what the table currently shows.
const filter = reactive({ league: '', startDate: '', endDate: '' });

async function load() {
  loading.value = true;
  error.value = '';
  try {
    const data = await listMatches(filter);
    matches.value = data.matches;
    // The server fills in the default range (next 7 days) on first load.
    Object.assign(filter, data.filter);
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}

function clearFilter() {
  Object.assign(filter, { league: '', startDate: '', endDate: '' });
  load();
}

function status(count) {
  if (count >= 3) return { label: '\u2713 Full', css: 'border-emerald-200 bg-emerald-50 text-emerald-700' };
  if (count > 0) return { label: '\u26A0 Partial', css: 'border-amber-200 bg-amber-50 text-amber-700' };
  return { label: '\u2717 None', css: 'border-red-200 bg-red-50 text-red-500' };
}

onMounted(async () => {
  leagues.value = (await getLookups().catch(() => ({ leagues: [] }))).leagues;
  load();
});
</script>

<template>
  <PageShell title="Upcoming Matches" :breadcrumbs="[{ text: 'Dashboard', to: '/dashboard' }, { text: 'Matches' }]">
    <template #actions>
      <RouterLink to="/matches/new" :class="btnPrimary">
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 6v6m0 0v6m0-6h6m-6 0H6" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
        </svg>
        <span>Add New Match</span>
      </RouterLink>
    </template>

    <section :class="[card, 'p-6']">
      <form class="grid grid-cols-1 items-end gap-4 sm:grid-cols-2 lg:grid-cols-4" @submit.prevent="load">
        <div class="space-y-1.5">
          <label for="league" class="block text-sm font-medium text-gray-600">League</label>
          <select id="league" v-model="filter.league" :class="inputClass()">
            <option value="">-- All Leagues --</option>
            <option v-for="l in leagues" :key="l" :value="l">{{ l }}</option>
          </select>
        </div>
        <div class="space-y-1.5">
          <label for="startDate" class="block text-sm font-medium text-gray-600">From Date</label>
          <input id="startDate" v-model="filter.startDate" type="date" :class="inputClass()" />
        </div>
        <div class="space-y-1.5">
          <label for="endDate" class="block text-sm font-medium text-gray-600">To Date</label>
          <input id="endDate" v-model="filter.endDate" type="date" :class="inputClass()" />
        </div>
        <div class="flex items-end gap-2">
          <button type="submit" :class="[btnPrimary, 'flex-1']">Filter</button>
          <button type="button" :class="btnOutline" @click="clearFilter">Clear</button>
        </div>
      </form>
    </section>

    <AsyncBlock :loading="loading" :error="error" loading-text="Loading matches..." @retry="load">
      <section :class="[card, 'overflow-hidden']">
        <div class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr :class="tableHead">
                <th class="px-6 py-3">Date</th>
                <th class="px-6 py-3">Match</th>
                <th class="px-6 py-3">Location</th>
                <th class="px-6 py-3">Referees</th>
                <th class="px-6 py-3">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-if="!matches.length">
                <td colspan="5" class="px-6 py-10 text-center text-sm text-gray-400">No matches found for the selected filters.</td>
              </tr>
              <tr v-for="m in matches" :key="m.id" :class="tableRow">
                <td class="px-6 py-4 text-sm">{{ formatDateTime(m.matchDate) }}</td>
                <td class="px-6 py-4 font-medium">{{ m.homeTeam }} vs {{ m.awayTeam }}</td>
                <td class="px-6 py-4 text-sm">{{ m.location }}</td>
                <td class="px-6 py-4">
                  <div class="flex flex-col gap-1.5">
                    <span
                      class="inline-flex w-fit items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-semibold"
                      :class="status(m.assignedCount).css"
                    >
                      {{ status(m.assignedCount).label }}
                    </span>
                    <span v-if="m.main" class="inline-flex items-center gap-1 text-xs text-gray-600">
                      <span class="w-6 font-semibold text-sky-600">M</span>{{ m.main }}
                    </span>
                    <span v-if="m.assistant1" class="inline-flex items-center gap-1 text-xs text-gray-600">
                      <span class="w-6 font-semibold text-indigo-500">A1</span>{{ m.assistant1 }}
                    </span>
                    <span v-if="m.assistant2" class="inline-flex items-center gap-1 text-xs text-gray-600">
                      <span class="w-6 font-semibold text-indigo-500">A2</span>{{ m.assistant2 }}
                    </span>
                  </div>
                </td>
                <td class="px-6 py-4">
                  <div class="flex items-center gap-2">
                    <RouterLink :to="`/matches/${m.id}/assign`" :class="btnPrimarySm">Assign Refs</RouterLink>
                    <RouterLink :to="`/matches/${m.id}/edit`" :class="btnOutlineSm">Edit</RouterLink>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </AsyncBlock>
  </PageShell>
</template>
