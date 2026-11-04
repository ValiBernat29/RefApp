<script setup>
import { ref, computed, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import ConfirmDialog from '../components/ui/ConfirmDialog.vue';
import { listTeams, deleteTeam } from '../api/teams.js';
import { showFlash } from '../flash.js';
import { LEGACY } from '../legacy.js';

const teams = ref([]);
const loading = ref(true);
const loadError = ref('');
const search = ref('');

const pendingDelete = ref(null);
const deleting = ref(false);

const visible = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) return teams.value;
  return teams.value.filter((t) =>
    [t.name, t.league, t.city ?? '', t.preferredMatchDay].some((v) => v.toLowerCase().includes(q)),
  );
});

async function load() {
  loading.value = true;
  loadError.value = '';
  try {
    teams.value = await listTeams();
  } catch (e) {
    loadError.value = e.message;
  } finally {
    loading.value = false;
  }
}

async function confirmDelete() {
  const team = pendingDelete.value;
  if (!team) return;
  deleting.value = true;
  try {
    const { deletedMatches } = await deleteTeam(team.id);
    teams.value = teams.value.filter((t) => t.id !== team.id);
    showFlash(
      deletedMatches > 0
        ? `Team ${team.name} and its ${deletedMatches} match${deletedMatches === 1 ? '' : 'es'} were deleted.`
        : `Team ${team.name} was deleted.`,
    );
  } catch (e) {
    showFlash(e.message, 'error');
  } finally {
    deleting.value = false;
    pendingDelete.value = null;
  }
}

onMounted(load);
</script>

<template>
  <PageShell title="Teams" :breadcrumbs="[{ text: 'Dashboard', href: LEGACY.dashboard }, { text: 'Teams' }]">
    <template #actions>
      <RouterLink
        to="/teams/new"
        class="inline-flex items-center space-x-2 rounded-lg bg-[#6eb7e4] px-5 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#5ca5d2]"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 6v6m0 0v6m0-6h6m-6 0H6" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
        </svg>
        <span>Add New Team</span>
      </RouterLink>
    </template>

    <div v-if="loading" class="flex items-center gap-3 py-10 text-gray-500">
      <div class="h-5 w-5 animate-spin rounded-full border-2 border-gray-300 border-t-sky-500"></div>
      Loading teams...
    </div>

    <div v-else-if="loadError" class="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700" role="alert">
      {{ loadError }}
      <button type="button" class="ml-2 font-medium underline" @click="load">Retry</button>
    </div>

    <section v-else class="overflow-hidden rounded-xl bg-white card-shadow">
      <div class="border-b border-gray-100 p-4">
        <input
          v-model="search"
          type="search"
          placeholder="Search by name, league, city or match day..."
          aria-label="Search teams"
          class="w-full max-w-md rounded-lg border-gray-200 px-3 py-2 text-sm focus:border-sky-400 focus:ring-sky-400"
        />
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr class="bg-gray-50 text-xs font-medium uppercase tracking-wider text-gray-500">
              <th class="px-6 py-3">Team Name</th>
              <th class="px-6 py-3">League</th>
              <th class="px-6 py-3">Match Day</th>
              <th class="px-6 py-3">Ground</th>
              <th class="px-6 py-3">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            <tr v-for="t in visible" :key="t.id" class="text-gray-700 transition-colors hover:bg-gray-50/50">
              <td class="px-6 py-4 font-medium">{{ t.name }}</td>
              <td class="px-6 py-4">
                <span class="inline-flex items-center rounded-full bg-sky-100 px-2 py-0.5 text-[11px] font-medium text-sky-600">{{ t.league }}</span>
              </td>
              <td class="px-6 py-4 text-sm">{{ t.preferredMatchDay }}</td>
              <td class="px-6 py-4 text-sm">
                <template v-if="t.city">
                  {{ t.city }}
                  <span
                    v-if="!t.hasCoordinates"
                    class="ml-1 rounded-full bg-amber-100 px-1.5 py-0.5 text-[10px] font-medium text-amber-700"
                    title="Map coordinates are still being looked up"
                  >
                    locating...
                  </span>
                </template>
                <span v-else class="text-gray-400">&mdash;</span>
              </td>
              <td class="px-6 py-4">
                <div class="flex items-center gap-2">
                  <RouterLink
                    :to="`/teams/${t.id}/edit`"
                    class="rounded-lg border border-[#91b5bf] px-4 py-1.5 text-sm font-medium text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]"
                  >
                    Edit
                  </RouterLink>
                  <button
                    type="button"
                    class="rounded-lg border border-red-200 bg-red-50 px-4 py-1.5 text-sm font-medium text-red-600 transition-colors hover:bg-red-100"
                    @click="pendingDelete = t"
                  >
                    Delete
                  </button>
                </div>
              </td>
            </tr>
            <tr v-if="!visible.length">
              <td colspan="5" class="px-6 py-10 text-center text-sm text-gray-400">
                {{ teams.length ? `No teams match "${search}".` : 'No teams yet. Add the first one.' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <ConfirmDialog
      :open="!!pendingDelete"
      :title="`Delete ${pendingDelete?.name ?? 'team'}?`"
      message="This permanently deletes the team, every match it plays in, and the referee assignments for those matches."
      confirm-text="Delete team"
      :busy="deleting"
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </PageShell>
</template>
