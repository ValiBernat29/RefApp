<script setup>
import { ref, computed, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import { listUsers, toggleCar } from '../api/users.js';
import { showFlash } from '../flash.js';
import { card, tableHead, tableRow } from '../components/ui/classes.js';

const referees = ref([]);
const loading = ref(true);
const error = ref('');

const withCar = computed(() => referees.value.filter((r) => r.hasCar).length);
const withoutCar = computed(() => referees.value.length - withCar.value);

async function load() {
  loading.value = true;
  error.value = '';
  try {
    referees.value = (await listUsers()).filter((u) => u.isReferee);
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}

// Optimistic update: flip immediately, undo if the server refuses.
async function onToggle(referee) {
  const before = referee.hasCar;
  referee.hasCar = !before;
  try {
    referee.hasCar = (await toggleCar(referee.id)).hasCar;
  } catch (e) {
    referee.hasCar = before;
    showFlash(`Failed to update car status. ${e.message}`, 'error');
  }
}

onMounted(load);
</script>

<template>
  <PageShell
    title="Referee Fleet"
    :breadcrumbs="[{ text: 'Dashboard', to: '/dashboard' }, { text: 'Users', to: '/users' }, { text: 'Fleet' }]"
  >
    <AsyncBlock :loading="loading" :error="error" loading-text="Loading referees..." @retry="load">
      <div class="space-y-8">
        <div class="grid grid-cols-1 gap-5 sm:grid-cols-3">
          <div :class="[card, 'flex items-center gap-4 p-5']">
            <div class="rounded-xl bg-sky-100 p-3">
              <svg class="h-6 w-6 text-sky-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M17 20H7m10 0v-2a3 3 0 00-3-3H9a3 3 0 00-3 3v2m12-10a4 4 0 10-8 0 4 4 0 008 0z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" />
              </svg>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">Total Referees</p>
              <p class="text-2xl font-bold text-gray-800">{{ referees.length }}</p>
            </div>
          </div>
          <div :class="[card, 'flex items-center gap-4 p-5']">
            <div class="rounded-xl bg-emerald-100 p-3">
              <svg class="h-6 w-6 text-emerald-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M5 17H3a2 2 0 01-2-2V9a2 2 0 012-2h1l2-4h8l2 4h1a2 2 0 012 2v6a2 2 0 01-2 2h-2m-6 0h4m-4 0a2 2 0 100 4 2 2 0 000-4zm4 0a2 2 0 100 4 2 2 0 000-4z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" />
              </svg>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">Have a Car</p>
              <p class="text-2xl font-bold text-emerald-600">{{ withCar }}</p>
            </div>
          </div>
          <div :class="[card, 'flex items-center gap-4 p-5']">
            <div class="rounded-xl bg-amber-100 p-3">
              <svg class="h-6 w-6 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" />
              </svg>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-gray-400">No Car</p>
              <p class="text-2xl font-bold text-amber-600">{{ withoutCar }}</p>
            </div>
          </div>
        </div>

        <div class="flex items-start gap-3 rounded-xl border border-sky-200 bg-sky-50 px-5 py-4 text-sm text-sky-800">
          <svg class="mt-0.5 h-5 w-5 flex-shrink-0 text-sky-500" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
          </svg>
          <span>
            The auto-allocation engine ensures <strong>at least one referee with a car</strong> is assigned per match.
            Use the toggles below to keep car availability up to date.
          </span>
        </div>

        <section :class="[card, 'overflow-hidden']">
          <div class="flex items-center justify-between border-b border-gray-100 px-6 py-5">
            <h2 class="text-lg font-bold text-gray-800">Referee Car Status</h2>
            <span class="text-xs text-gray-400">Toggle saves automatically</span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-left">
              <thead>
                <tr :class="tableHead">
                  <th class="px-6 py-3">Name</th>
                  <th class="px-6 py-3">Rank</th>
                  <th class="px-6 py-3">Home City</th>
                  <th class="px-6 py-3">Car Status</th>
                  <th class="px-6 py-3">Toggle</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                <tr v-if="!referees.length">
                  <td colspan="5" class="px-6 py-10 text-center text-sm text-gray-400">No referees found.</td>
                </tr>
                <tr v-for="r in referees" :key="r.id" :class="tableRow">
                  <td class="px-6 py-4 font-medium">{{ r.displayName || r.userName || '\u2014' }}</td>
                  <td class="px-6 py-4 text-sm">
                    <span class="inline-block rounded-full border border-sky-200 bg-sky-50 px-2 py-0.5 text-xs font-semibold text-sky-700">{{ r.rank }}</span>
                  </td>
                  <td class="px-6 py-4 text-sm text-gray-500">{{ r.homeCity || '\u2014' }}</td>
                  <td class="px-6 py-4">
                    <span
                      v-if="r.hasCar"
                      class="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700"
                    >
                      Has Car
                    </span>
                    <span
                      v-else
                      class="inline-flex items-center gap-1.5 rounded-full border border-gray-200 bg-gray-100 px-2.5 py-1 text-xs font-semibold text-gray-500"
                    >
                      No Car
                    </span>
                  </td>
                  <td class="px-6 py-4">
                    <button
                      type="button"
                      role="switch"
                      :aria-checked="r.hasCar"
                      :aria-label="`Toggle car for ${r.displayName || r.userName}`"
                      class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-sky-400 focus:ring-offset-2"
                      :class="r.hasCar ? 'bg-emerald-500' : 'bg-gray-300'"
                      :title="r.hasCar ? 'Click to mark as no car' : 'Click to mark as has car'"
                      @click="onToggle(r)"
                    >
                      <span
                        class="inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform duration-200"
                        :class="r.hasCar ? 'translate-x-6' : 'translate-x-1'"
                      ></span>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </AsyncBlock>
  </PageShell>
</template>
