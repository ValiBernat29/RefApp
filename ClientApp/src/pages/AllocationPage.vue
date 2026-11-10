<script setup>
import { ref, reactive } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import { runAllocation } from '../api/board.js';
import { formatDateTime, formatDate, formatIsoDate } from '../format.js';
import { btnPrimary, btnOutline, card, inputClass, tableHead, tableRow } from '../components/ui/classes.js';

const today = new Date();
const inAWeek = new Date(today.getTime() + 7 * 24 * 60 * 60 * 1000);
const form = reactive({ startDate: formatIsoDate(today), endDate: formatIsoDate(inAWeek) });

const running = ref(false);
const error = ref('');
const outcome = ref(null); // { result, startDate, endDate, matches } once the solver has run

async function run() {
  error.value = '';
  if (!form.startDate || !form.endDate) {
    error.value = 'Please choose both dates.';
    return;
  }
  if (form.endDate < form.startDate) {
    error.value = 'End date must be on or after the start date.';
    return;
  }
  running.value = true;
  try {
    outcome.value = await runAllocation({ startDate: form.startDate, endDate: form.endDate });
  } catch (e) {
    error.value = e.message;
  } finally {
    running.value = false;
  }
}

function status(count) {
  if (count >= 3) return { label: '\u2713 Full', css: 'border-emerald-200 bg-emerald-50 text-emerald-700' };
  if (count > 0) return { label: '\u26A0 Partial', css: 'border-amber-200 bg-amber-50 text-amber-700' };
  return { label: '\u2717 Unassigned', css: 'border-red-200 bg-red-50 text-red-500' };
}

const steps = [
  'Fetches all matches & referees in the selected range in a single bulk query.',
  'Enforces hard constraints: availability, conflict of interest, 21-day history, and certification rank.',
  'Minimises soft-constraint penalties: travel distance (>40 km flagged) & monthly workload balance.',
  'Saves the optimal schedule directly to the database. You can manually adjust any assignment afterwards.',
];
</script>

<template>
  <!-- Results -->
  <PageShell
    v-if="outcome"
    title="Allocation Results"
    :breadcrumbs="[
      { text: 'Dashboard', to: '/dashboard' },
      { text: 'Auto-Allocate' },
      { text: 'Results' },
    ]"
  >
    <template #actions>
      <div class="flex items-center gap-3">
        <button type="button" :class="btnOutline" @click="outcome = null">&larr; Run Again</button>
        <RouterLink to="/matches" :class="btnPrimary">View All Matches &rarr;</RouterLink>
      </div>
    </template>

    <div class="space-y-8">
      <div class="grid grid-cols-2 gap-5 sm:grid-cols-4">
        <div :class="[card, 'flex flex-col gap-1 p-5']">
          <span class="text-xs font-semibold uppercase tracking-wider text-gray-400">Period</span>
          <span class="text-sm font-bold text-gray-800">{{ formatDate(outcome.startDate) }} &ndash; {{ formatDate(outcome.endDate) }}</span>
        </div>
        <div :class="[card, 'flex flex-col gap-1 p-5']">
          <span class="text-xs font-semibold uppercase tracking-wider text-gray-400">Matches Processed</span>
          <span class="text-3xl font-extrabold text-sky-600">{{ outcome.result.totalMatchesToAssign }}</span>
        </div>
        <div :class="[card, 'flex flex-col gap-1 p-5']">
          <span class="text-xs font-semibold uppercase tracking-wider text-gray-400">Fully Assigned</span>
          <span class="text-3xl font-extrabold text-emerald-600">{{ outcome.result.fullyAssignedMatchesCount }}</span>
        </div>
        <div :class="[card, 'flex flex-col gap-1 p-5']">
          <span class="text-xs font-semibold uppercase tracking-wider text-gray-400">Avg Travel</span>
          <span class="text-3xl font-extrabold text-gray-800">
            {{ outcome.result.avgTravelDistanceKm }} <span class="text-base font-medium text-gray-400">km</span>
          </span>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-5 sm:grid-cols-4">
        <div :class="[card, 'flex items-center gap-4 p-5']">
          <div class="rounded-lg bg-amber-100 p-3 text-amber-600">&#9888;</div>
          <div>
            <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">Partially Assigned</p>
            <p class="text-2xl font-bold text-amber-600">{{ outcome.result.partiallyAssignedMatchesCount }}</p>
          </div>
        </div>
        <div :class="[card, 'flex items-center gap-4 p-5']">
          <div class="rounded-lg bg-sky-100 p-3 text-sky-600">&#128205;</div>
          <div>
            <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">Total Travel</p>
            <p class="text-2xl font-bold text-sky-700">
              {{ outcome.result.totalTravelDistanceKm }} <span class="text-sm font-medium text-gray-400">km</span>
            </p>
          </div>
        </div>
        <div :class="[card, 'flex items-center gap-4 p-5']">
          <div class="rounded-lg p-3" :class="outcome.result.noCarWarningsCount > 0 ? 'bg-orange-100 text-orange-500' : 'bg-emerald-100 text-emerald-600'">&#128663;</div>
          <div>
            <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">No-Car Matches</p>
            <p class="text-2xl font-bold" :class="outcome.result.noCarWarningsCount > 0 ? 'text-orange-500' : 'text-emerald-600'">
              {{ outcome.result.noCarWarningsCount }}
            </p>
          </div>
        </div>
        <div :class="[card, 'flex items-center gap-4 p-5']">
          <div class="rounded-lg p-3" :class="outcome.result.success ? 'bg-emerald-100 text-emerald-600' : 'bg-red-100 text-red-500'">
            {{ outcome.result.success ? '\u2713' : '\u2717' }}
          </div>
          <div>
            <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">Status</p>
            <p v-if="outcome.result.success" class="text-sm font-semibold text-emerald-700">Completed Successfully</p>
            <p v-else class="text-sm font-semibold text-red-600">{{ outcome.result.message }}</p>
          </div>
        </div>
      </div>

      <section v-if="outcome.result.warnings.length" class="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-6">
        <h2 class="flex items-center gap-2 font-bold text-amber-800">{{ outcome.result.warnings.length }} Unresolved Slot(s)</h2>
        <ul class="space-y-1.5 text-sm text-amber-900">
          <li v-for="w in outcome.result.warnings" :key="w" class="flex items-start gap-2">
            <span class="mt-0.5 text-amber-500">&bull;</span>{{ w }}
          </li>
        </ul>
      </section>

      <section :class="[card, 'overflow-hidden']">
        <div class="border-b border-gray-100 px-6 py-5">
          <h2 class="text-lg font-bold text-gray-800">
            Match Schedule &mdash; {{ formatDate(outcome.startDate) }} to {{ formatDate(outcome.endDate) }}
          </h2>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr :class="tableHead">
                <th class="px-6 py-3">Date</th>
                <th class="px-6 py-3">Match</th>
                <th class="px-6 py-3">League</th>
                <th class="px-6 py-3">Main Referee</th>
                <th class="px-6 py-3">Assistant 1</th>
                <th class="px-6 py-3">Assistant 2</th>
                <th class="px-6 py-3">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-if="!outcome.matches.length">
                <td colspan="7" class="px-6 py-10 text-center text-sm text-gray-400">No matches found in the selected date range.</td>
              </tr>
              <tr v-for="m in outcome.matches" :key="m.id" :class="tableRow">
                <td class="whitespace-nowrap px-6 py-4 text-sm">{{ formatDateTime(m.matchDate) }}</td>
                <td class="px-6 py-4 font-medium">{{ m.homeTeam }} vs {{ m.awayTeam }}</td>
                <td class="px-6 py-4 text-sm">
                  <span class="inline-block rounded-full border border-sky-200 bg-sky-50 px-2 py-0.5 text-xs font-semibold text-sky-700">{{ m.league || '\u2014' }}</span>
                </td>
                <td v-for="(slot, i) in [m.main, m.assistant1, m.assistant2]" :key="i" class="px-6 py-4 text-sm">
                  <span v-if="slot" class="flex items-center gap-1" :class="i === 0 ? 'font-medium text-sky-700' : 'text-indigo-700'">
                    {{ slot.name }}
                    <span v-if="slot.hasCar" title="Has a car" class="text-emerald-500">&#128663;</span>
                  </span>
                  <span v-else class="text-xs text-red-400">Not assigned</span>
                </td>
                <td class="px-6 py-4">
                  <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold" :class="status(m.assignedCount).css">
                    {{ status(m.assignedCount).label }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  </PageShell>

  <!-- Launch form -->
  <PageShell
    v-else
    title="Smart Auto-Allocation"
    :breadcrumbs="[{ text: 'Dashboard', to: '/dashboard' }, { text: 'Auto-Allocate' }]"
  >
    <p v-if="error" class="rounded-xl border border-red-200 bg-red-50 px-5 py-3 text-sm text-red-700" role="alert">{{ error }}</p>

    <div class="grid grid-cols-1 gap-8 lg:grid-cols-3">
      <div :class="[card, 'space-y-6 p-8 lg:col-span-2']">
        <div class="mb-2 flex items-center gap-3">
          <div class="rounded-xl bg-sky-100 p-3 text-2xl text-sky-600" aria-hidden="true">&#128161;</div>
          <div>
            <h2 class="text-xl font-bold text-gray-800">Run Allocation Engine</h2>
            <p class="text-sm text-gray-500">Automatically assigns referees using the Branch &amp; Bound optimisation solver.</p>
          </div>
        </div>

        <form class="space-y-5" @submit.prevent="run">
          <div class="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div class="space-y-1.5">
              <label for="startDate" class="block text-sm font-medium text-gray-600">From Date</label>
              <input id="startDate" v-model="form.startDate" type="date" :class="inputClass()" />
            </div>
            <div class="space-y-1.5">
              <label for="endDate" class="block text-sm font-medium text-gray-600">To Date</label>
              <input id="endDate" v-model="form.endDate" type="date" :class="inputClass()" />
            </div>
          </div>

          <div class="pt-2">
            <button id="run-allocation-btn" type="submit" :class="[btnPrimary, 'rounded-xl px-7 py-3 font-semibold']" :disabled="running">
              <svg v-if="running" class="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24" aria-hidden="true">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
              </svg>
              <svg v-else class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M13 10V3L4 14h7v7l9-11h-7z" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
              {{ running ? 'Running\u2026' : 'Run Auto-Allocation' }}
            </button>
          </div>
        </form>
      </div>

      <aside class="space-y-4">
        <section :class="[card, 'space-y-4 p-6']">
          <h3 class="text-base font-bold text-gray-800">How It Works</h3>
          <ul class="space-y-3 text-sm text-gray-600">
            <li v-for="(step, i) in steps" :key="i" class="flex items-start gap-2">
              <span class="mt-0.5 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full bg-emerald-100 text-xs font-bold text-emerald-600">{{ i + 1 }}</span>
              <span>{{ step }}</span>
            </li>
          </ul>
        </section>

        <section class="space-y-1 rounded-xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-800">
          <p class="font-semibold">&#9888; Note</p>
          <p>Running auto-allocation will <strong>overwrite</strong> any existing referee assignments for matches in the selected date range.</p>
        </section>
      </aside>
    </div>
  </PageShell>
</template>
