<script setup>
import { ref, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import { listUnavailability } from '../api/board.js';
import { formatIsoDate } from '../format.js';
import { card, tableHead, tableRow } from '../components/ui/classes.js';

const roster = ref([]);
const loading = ref(true);
const error = ref('');

async function load() {
  loading.value = true;
  error.value = '';
  try {
    roster.value = await listUnavailability();
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}
onMounted(load);
</script>

<template>
  <PageShell title="Unavailability Roster" :breadcrumbs="[{ text: 'Dashboard', to: '/dashboard' }, { text: 'Unavailability' }]">
    <AsyncBlock :loading="loading" :error="error" loading-text="Loading roster..." @retry="load">
      <section :class="[card, 'overflow-hidden']">
        <div v-if="!roster.length" class="px-6 py-12 text-center text-sm text-gray-400">No unavailability records.</div>
        <div v-else class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr :class="tableHead">
                <th class="px-6 py-3">Referee</th>
                <th class="px-6 py-3">Start Date</th>
                <th class="px-6 py-3">End Date</th>
                <th class="px-6 py-3">Reason</th>
                <th class="px-6 py-3">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-for="u in roster" :key="u.id" :class="tableRow">
                <td class="px-6 py-4 font-medium">{{ u.refereeName }}</td>
                <td class="px-6 py-4 text-sm">{{ formatIsoDate(u.startDate) }}</td>
                <td class="px-6 py-4 text-sm">{{ formatIsoDate(u.endDate) }}</td>
                <td class="px-6 py-4 text-sm text-gray-500">{{ u.reason || '\u2014' }}</td>
                <td class="px-6 py-4">
                  <span class="inline-flex items-center space-x-1.5 rounded-lg bg-[#94879c] px-3 py-1 text-xs font-medium text-white">
                    <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
                    </svg>
                    <span>Unavailable</span>
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </AsyncBlock>
  </PageShell>
</template>
