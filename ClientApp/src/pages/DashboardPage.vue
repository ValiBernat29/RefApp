<script setup>
import { ref, onMounted } from 'vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import { getDashboard } from '../api/board.js';
import { formatLongDateTime, formatMonthDay, formatMonthDayYear } from '../format.js';
import { btnPrimarySm, tableHead, tableRow } from '../components/ui/classes.js';

const data = ref(null);
const loading = ref(true);
const error = ref('');

async function load() {
  loading.value = true;
  error.value = '';
  try {
    data.value = await getDashboard();
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}
onMounted(load);

const quickActionBase = 'group flex w-full items-center space-x-4 rounded-xl px-4 py-4 text-left';
const quickActionOutline = `${quickActionBase} border border-[#91b5bf] text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]`;
const quickActionIcon = 'rounded-md border border-gray-100 bg-gray-50 p-1 group-hover:bg-white';
</script>

<template>
  <main class="mx-auto grid max-w-[1440px] grid-cols-12 gap-8 p-8">
    <div class="col-span-12 space-y-8 lg:col-span-9">
      <div>
        <h1 class="text-2xl font-bold leading-tight text-gray-900">Match Overview</h1>
        <nav class="mt-0.5 text-sm text-gray-400" aria-label="Breadcrumb">
          <span>Dashboard</span><span class="mx-1">&bull;</span><span>Board</span>
        </nav>
      </div>

      <AsyncBlock :loading="loading" :error="error" loading-text="Loading dashboard..." @retry="load">
        <template v-if="data">
          <!-- Summary cards -->
          <div class="grid grid-cols-1 gap-6 sm:grid-cols-2">
            <div class="flex items-start justify-between rounded-xl bg-[#79badb] p-6 text-white card-shadow">
              <div>
                <div class="mb-1 text-6xl font-extrabold">{{ data.upcomingMatchesCount }}</div>
                <div class="font-medium text-blue-50">Upcoming matches this week</div>
              </div>
              <div class="rounded-full bg-white/20 p-2">
                <svg class="h-10 w-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" />
                </svg>
              </div>
            </div>
            <div class="flex items-start justify-between rounded-xl bg-[#79badb] p-6 text-white card-shadow">
              <div>
                <div class="mb-1 text-6xl font-extrabold">{{ data.matchesNeedingReferees }}</div>
                <div class="font-medium text-blue-50">Matches needing referees</div>
              </div>
              <div class="rounded-full bg-white/20 p-2">
                <svg class="h-10 w-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" />
                </svg>
              </div>
            </div>
          </div>

          <!-- Pending assignments -->
          <section class="overflow-hidden rounded-xl bg-white card-shadow">
            <div class="flex items-center justify-between border-b border-gray-100 px-6 py-5">
              <h2 class="text-xl font-bold text-gray-800">Pending Assignments</h2>
              <RouterLink to="/matches" class="text-sm font-medium text-sky-600 transition-colors hover:text-sky-700">View All &rarr;</RouterLink>
            </div>
            <div class="overflow-x-auto">
              <table v-if="data.pendingMatches.length" class="w-full text-left">
                <thead>
                  <tr :class="tableHead">
                    <th class="px-6 py-3">Match</th>
                    <th class="px-6 py-3">Date &amp; Time</th>
                    <th class="px-6 py-3">Venue</th>
                    <th class="px-6 py-3">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                  <tr v-for="m in data.pendingMatches" :key="m.id" :class="tableRow">
                    <td class="px-6 py-4 font-medium">{{ m.homeTeam }} vs {{ m.awayTeam }}</td>
                    <td class="px-6 py-4">{{ formatLongDateTime(m.matchDate) }}</td>
                    <td class="px-6 py-4">{{ m.location }}</td>
                    <td class="px-6 py-4">
                      <RouterLink :to="`/matches/${m.id}/assign`" :class="btnPrimarySm">Assign</RouterLink>
                    </td>
                  </tr>
                </tbody>
              </table>
              <div v-else class="px-6 py-10 text-center text-sm text-gray-400">
                No upcoming matches.
                <RouterLink to="/matches/new" class="ml-1 text-sky-600 hover:underline">Create one</RouterLink>.
              </div>
            </div>
          </section>

          <!-- Unavailability roster -->
          <section class="overflow-hidden rounded-xl bg-white card-shadow">
            <div class="flex items-center justify-between border-b border-gray-100 px-6 py-5">
              <h2 class="text-xl font-bold text-gray-800">Unavailability Roster</h2>
              <RouterLink to="/unavailability" class="text-sm font-medium text-sky-600 transition-colors hover:text-sky-700">View All &rarr;</RouterLink>
            </div>
            <div class="space-y-1 p-6">
              <div
                v-for="u in data.recentUnavailabilities"
                :key="u.id"
                class="flex items-center justify-between border-b border-gray-50 py-3 last:border-0"
              >
                <span class="w-1/3 font-medium text-gray-700">{{ u.refereeName }}</span>
                <div class="flex w-1/3 items-center text-sm text-gray-500">
                  <svg class="mr-2 h-4 w-4 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" />
                  </svg>
                  <span>{{ formatMonthDay(u.startDate) }} &ndash; {{ formatMonthDayYear(u.endDate) }}</span>
                </div>
                <div class="flex w-1/3 justify-end">
                  <span class="inline-flex items-center space-x-2 rounded-lg bg-[#94879c] px-4 py-1.5 text-sm font-medium text-white">
                    <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
                    </svg>
                    <span>Unavailable</span>
                  </span>
                </div>
              </div>
              <p v-if="!data.recentUnavailabilities.length" class="py-4 text-center text-sm text-gray-400">
                No current unavailability records.
              </p>
            </div>
          </section>
        </template>
      </AsyncBlock>
    </div>

    <!-- Sidebar -->
    <aside class="col-span-12 lg:col-span-3">
      <section class="rounded-xl bg-white p-6 card-shadow">
        <h2 class="mb-6 text-xl font-bold text-gray-800">Quick Actions</h2>
        <div class="flex flex-col space-y-4">
          <RouterLink to="/matches/new" :class="quickActionOutline">
            <div :class="quickActionIcon">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 6v6m0 0v6m0-6h6m-6 0H6" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
            </div>
            <span class="font-medium text-gray-600">Create Match</span>
          </RouterLink>

          <RouterLink to="/users" :class="quickActionOutline">
            <div :class="quickActionIcon">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
            </div>
            <span class="font-medium text-gray-600">Manage Users</span>
          </RouterLink>

          <RouterLink to="/teams" :class="quickActionOutline">
            <div :class="quickActionIcon">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
            </div>
            <span class="font-medium text-gray-600">Manage Teams</span>
          </RouterLink>

          <RouterLink to="/allocation" :class="`${quickActionBase} bg-[#6eb7e4] text-white transition-colors hover:bg-[#5ca5d2]`">
            <div class="rounded-md border border-white/20 bg-white/20 p-1 group-hover:bg-white/30">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M13 10V3L4 14h7v7l9-11h-7z" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
            </div>
            <span class="font-medium">Auto-Allocate Referees</span>
          </RouterLink>
        </div>
      </section>
    </aside>
  </main>
</template>
