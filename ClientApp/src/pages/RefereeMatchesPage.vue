<script setup>
import { ref, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import { listMyMatches } from '../api/referee.js';
import { formatDateTime } from '../format.js';
import { badgeSky, card, tableHead, tableRow } from '../components/ui/classes.js';

const matches = ref([]);
const loading = ref(true);
const error = ref('');

async function load() {
  loading.value = true;
  error.value = '';
  try {
    matches.value = await listMyMatches();
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}
onMounted(load);
</script>

<template>
  <PageShell title="My Matches" :breadcrumbs="[{ text: 'Dashboard', to: '/referee' }, { text: 'My Matches' }]">
    <p class="-mt-4 text-sm text-gray-500">View your upcoming and past assignments, and see your referee team.</p>

    <AsyncBlock :loading="loading" :error="error" loading-text="Loading your matches..." @retry="load">
      <section :class="[card, 'overflow-hidden']">
        <div class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr :class="tableHead">
                <th class="px-6 py-3">Date &amp; Time</th>
                <th class="px-6 py-3">Match</th>
                <th class="px-6 py-3">Location</th>
                <th class="px-6 py-3">Referee Team</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-if="!matches.length">
                <td colspan="4" class="px-6 py-10 text-center text-sm text-gray-400">You have no match assignments yet.</td>
              </tr>
              <tr v-for="m in matches" :key="m.id" :class="tableRow">
                <td class="px-6 py-4 text-sm">{{ formatDateTime(m.matchDate) }}</td>
                <td class="px-6 py-4 font-medium">{{ m.homeTeam }} vs {{ m.awayTeam }}</td>
                <td class="px-6 py-4 text-sm">{{ m.location }}</td>
                <td class="px-6 py-4">
                  <div class="space-y-1">
                    <div v-for="member in m.team" :key="member.role" class="flex items-center gap-2 text-sm">
                      <span :class="badgeSky">{{ member.role }}</span>
                      <span class="text-gray-600">{{ member.name }}</span>
                    </div>
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
