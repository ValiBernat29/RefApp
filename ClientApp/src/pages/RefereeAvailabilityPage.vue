<script setup>
import { ref, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import ConfirmDialog from '../components/ui/ConfirmDialog.vue';
import { listMyUnavailability, deleteMyUnavailability } from '../api/referee.js';
import { showFlash } from '../flash.js';
import { formatIsoDate } from '../format.js';
import { btnPrimary, btnOutlineSm, btnDangerSm, card, tableHead, tableRow } from '../components/ui/classes.js';

const items = ref([]);
const loading = ref(true);
const error = ref('');
const pendingDelete = ref(null);
const deleting = ref(false);

async function load() {
  loading.value = true;
  error.value = '';
  try {
    items.value = await listMyUnavailability();
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}

async function confirmDelete() {
  const item = pendingDelete.value;
  deleting.value = true;
  try {
    await deleteMyUnavailability(item.id);
    items.value = items.value.filter((u) => u.id !== item.id);
    showFlash('Unavailability removed.');
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
  <PageShell title="My Availability" :breadcrumbs="[{ text: 'Dashboard', to: '/referee' }, { text: 'My Availability' }]">
    <template #actions>
      <RouterLink to="/referee/availability/new" :class="btnPrimary">
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 6v6m0 0v6m0-6h6m-6 0H6" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
        </svg>
        <span>Add Unavailability</span>
      </RouterLink>
    </template>

    <AsyncBlock :loading="loading" :error="error" @retry="load">
      <section :class="[card, 'overflow-hidden']">
        <div v-if="!items.length" class="px-6 py-12 text-center text-sm text-gray-400">
          No blocked dates.
          <RouterLink to="/referee/availability/new" class="ml-1 text-sky-600 hover:underline">Add unavailability</RouterLink>.
        </div>
        <div v-else class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr :class="tableHead">
                <th class="px-6 py-3">Start</th>
                <th class="px-6 py-3">End</th>
                <th class="px-6 py-3">Reason</th>
                <th class="px-6 py-3">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-for="u in items" :key="u.id" :class="tableRow">
                <td class="px-6 py-4 text-sm">{{ formatIsoDate(u.startDate) }}</td>
                <td class="px-6 py-4 text-sm">{{ formatIsoDate(u.endDate) }}</td>
                <td class="px-6 py-4 text-sm text-gray-500">{{ u.reason || '\u2014' }}</td>
                <td class="px-6 py-4">
                  <div class="flex items-center gap-2">
                    <RouterLink :to="`/referee/availability/${u.id}/edit`" :class="btnOutlineSm">Edit</RouterLink>
                    <button type="button" :class="btnDangerSm" @click="pendingDelete = u">Delete</button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </AsyncBlock>

    <ConfirmDialog
      :open="!!pendingDelete"
      title="Delete this unavailability?"
      :message="pendingDelete ? `This permanently removes the blocked period ${formatIsoDate(pendingDelete.startDate)} to ${formatIsoDate(pendingDelete.endDate)}.` : ''"
      confirm-text="Delete"
      :busy="deleting"
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </PageShell>
</template>
