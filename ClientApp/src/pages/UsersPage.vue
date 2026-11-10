<script setup>
import { ref, onMounted } from 'vue';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import ConfirmDialog from '../components/ui/ConfirmDialog.vue';
import { listUsers, deleteUser, toggleCar } from '../api/users.js';
import { showFlash } from '../flash.js';
import { btnPrimary, btnOutlineSm, btnDangerSm, card, tableHead, tableRow } from '../components/ui/classes.js';

const users = ref([]);
const loading = ref(true);
const error = ref('');
const busyCar = ref(new Set()); // user ids with a toggle in flight

const pendingDelete = ref(null);
const deleting = ref(false);

async function load() {
  loading.value = true;
  error.value = '';
  try {
    users.value = await listUsers();
  } catch (e) {
    error.value = e.message;
  } finally {
    loading.value = false;
  }
}

async function onToggleCar(user) {
  busyCar.value = new Set(busyCar.value).add(user.id);
  try {
    user.hasCar = (await toggleCar(user.id)).hasCar;
  } catch (e) {
    showFlash(e.message, 'error');
  } finally {
    const next = new Set(busyCar.value);
    next.delete(user.id);
    busyCar.value = next;
  }
}

async function confirmDelete() {
  const user = pendingDelete.value;
  deleting.value = true;
  try {
    await deleteUser(user.id);
    users.value = users.value.filter((u) => u.id !== user.id);
    showFlash('Account and all associated records deleted successfully.');
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
  <PageShell title="System Users" :breadcrumbs="[{ text: 'Dashboard', to: '/dashboard' }, { text: 'Users' }]">
    <template #actions>
      <div class="flex items-center gap-3">
        <RouterLink
          to="/users/fleet"
          class="rounded-lg border border-[#91b5bf] px-5 py-2.5 text-sm font-medium text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]"
        >
          Referee Fleet
        </RouterLink>
        <!-- Creating accounts stays on the built-in ASP.NET Identity registration page. -->
        <a href="/Identity/Account/Register" :class="btnPrimary">
          <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M12 6v6m0 0v6m0-6h6m-6 0H6" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
          </svg>
          <span>Add New User</span>
        </a>
      </div>
    </template>

    <AsyncBlock :loading="loading" :error="error" loading-text="Loading users..." @retry="load">
      <section :class="[card, 'overflow-hidden']">
        <div class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr :class="tableHead">
                <th class="px-6 py-3">Username</th>
                <th class="px-6 py-3">Display Name</th>
                <th class="px-6 py-3">Role</th>
                <th class="px-6 py-3">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-for="u in users" :key="u.id" :class="tableRow">
                <td class="px-6 py-4 text-sm">{{ u.userName }}</td>
                <td class="px-6 py-4 font-medium">{{ u.displayName || '\u2014' }}</td>
                <td class="px-6 py-4">
                  <span
                    class="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium"
                    :class="u.role.includes('Board') ? 'bg-amber-100 text-amber-600' : 'bg-sky-100 text-sky-600'"
                  >
                    {{ u.role }}
                  </span>
                </td>
                <td class="px-6 py-4">
                  <div class="flex items-center gap-2">
                    <RouterLink :to="`/users/${u.id}/edit`" :class="btnOutlineSm">Edit</RouterLink>

                    <button
                      v-if="u.isReferee"
                      type="button"
                      class="inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs font-semibold transition-all duration-200 disabled:opacity-60"
                      :class="
                        u.hasCar
                          ? 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                          : 'border-gray-200 bg-gray-50 text-gray-500 hover:bg-gray-100'
                      "
                      :disabled="busyCar.has(u.id)"
                      :title="u.hasCar ? 'Has a car \u2014 click to remove' : 'No car \u2014 click to assign'"
                      @click="onToggleCar(u)"
                    >
                      <svg class="h-3.5 w-3.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                        <path d="M5 17H3a2 2 0 01-2-2V9a2 2 0 012-2h1l2-4h8l2 4h1a2 2 0 012 2v6a2 2 0 01-2 2h-2m-6 0h4m-4 0a2 2 0 100 4 2 2 0 000-4zm4 0a2 2 0 100 4 2 2 0 000-4z" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" />
                      </svg>
                      <span>{{ u.hasCar ? 'Has Car' : 'No Car' }}</span>
                    </button>

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
      :title="`Delete ${pendingDelete?.userName ?? 'account'}?`"
      message="This permanently deletes the account, its referee assignments and its unavailability records."
      confirm-text="Delete account"
      :busy="deleting"
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </PageShell>
</template>
