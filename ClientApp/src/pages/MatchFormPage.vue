<script setup>
import { ref, reactive, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import ConfirmDialog from '../components/ui/ConfirmDialog.vue';
import FormField from '../components/ui/FormField.vue';
import { getMatch, createMatch, updateMatch, deleteMatch } from '../api/matches.js';
import { listTeams } from '../api/teams.js';
import { getLookups } from '../api/lookups.js';
import { showFlash } from '../flash.js';
import { toLocalInputValue, withSeconds } from '../format.js';
import { btnPrimary, btnOutline, btnDanger, card, inputClass } from '../components/ui/classes.js';

// Present when editing (/matches/:id/edit), absent when creating (/matches/new).
const props = defineProps({ id: { type: Number, default: null } });

const router = useRouter();
const isEdit = computed(() => props.id !== null);

const leagues = ref([]);
const teams = ref([]);
const form = reactive({ league: '', matchDate: '', homeTeamId: '', awayTeamId: '' });
const errors = ref({});
const formError = ref('');
const loading = ref(true);
const loadError = ref('');
const saving = ref(false);
const confirmingDelete = ref(false);
const deleting = ref(false);

// Only offer teams from the chosen league (all teams while no league is chosen).
const teamOptions = computed(() =>
  teams.value.filter((t) => !form.league || t.league === form.league).sort((a, b) => a.name.localeCompare(b.name)),
);

// Matches are scheduled on the hour.
const snapToHour = () => {
  if (form.matchDate) form.matchDate = toLocalInputValue(form.matchDate);
};

function validate() {
  const found = {};
  if (!form.matchDate) found.matchDate = 'Please choose the match date and time.';
  if (!form.homeTeamId) found.homeTeamId = 'Please select the home team.';
  if (!form.awayTeamId) found.awayTeamId = 'Please select the away team.';
  errors.value = found;
  return Object.keys(found).length === 0;
}

async function submit() {
  formError.value = '';
  if (!validate()) return;
  saving.value = true;
  const payload = {
    matchDate: withSeconds(form.matchDate),
    homeTeamId: Number(form.homeTeamId),
    awayTeamId: Number(form.awayTeamId),
  };
  try {
    if (isEdit.value) await updateMatch(props.id, payload);
    else await createMatch(payload);
    showFlash(isEdit.value ? 'Match updated successfully.' : 'Match created successfully.');
    router.push('/matches');
  } catch (e) {
    errors.value = e.fieldErrors ?? {};
    if (!Object.keys(errors.value).length) formError.value = e.message;
    saving.value = false;
  }
}

async function confirmDelete() {
  deleting.value = true;
  try {
    const { name } = await deleteMatch(props.id);
    showFlash(`Match ${name} deleted successfully.`);
    router.push('/matches');
  } catch (e) {
    confirmingDelete.value = false;
    formError.value = e.message;
    deleting.value = false;
  }
}

onMounted(async () => {
  try {
    const [lookups, allTeams] = await Promise.all([getLookups(), listTeams()]);
    leagues.value = lookups.leagues;
    teams.value = allTeams;

    if (isEdit.value) {
      const match = await getMatch(props.id);
      Object.assign(form, {
        league: match.league ?? '',
        matchDate: toLocalInputValue(match.matchDate, false),
        homeTeamId: String(match.homeTeamId),
        awayTeamId: String(match.awayTeamId),
      });
    } else {
      const next = new Date();
      next.setHours(next.getHours() + 1);
      form.matchDate = toLocalInputValue(next);
    }
  } catch (e) {
    loadError.value = e.message;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <PageShell
    :title="isEdit ? 'Edit Match' : 'Create Match'"
    :breadcrumbs="[
      { text: 'Dashboard', to: '/dashboard' },
      { text: 'Matches', to: '/matches' },
      { text: isEdit ? 'Edit' : 'Create' },
    ]"
  >
    <AsyncBlock :loading="loading" :error="loadError">
      <section :class="[card, 'max-w-xl p-6']">
        <form class="space-y-5" novalidate @submit.prevent="submit">
          <p v-if="formError" class="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-600" role="alert">
            {{ formError }}
          </p>

          <FormField label="League" for="match-league">
            <select id="match-league" v-model="form.league" :class="inputClass()">
              <option value="">All leagues</option>
              <option v-for="l in leagues" :key="l" :value="l">{{ l }}</option>
            </select>
          </FormField>

          <FormField label="Match Date" for="match-date" :error="errors.matchDate">
            <input
              id="match-date"
              v-model="form.matchDate"
              type="datetime-local"
              step="3600"
              :class="inputClass(!!errors.matchDate)"
              @change="snapToHour"
              @blur="snapToHour"
            />
          </FormField>

          <FormField label="Home Team" for="match-home" :error="errors.homeTeamId">
            <select id="match-home" v-model="form.homeTeamId" :class="inputClass(!!errors.homeTeamId)">
              <option value="">Select home team</option>
              <option v-for="t in teamOptions" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </FormField>

          <FormField label="Away Team" for="match-away" :error="errors.awayTeamId">
            <select id="match-away" v-model="form.awayTeamId" :class="inputClass(!!errors.awayTeamId)">
              <option value="">Select away team</option>
              <option v-for="t in teamOptions" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </FormField>

          <div class="flex items-center gap-3 pt-2">
            <button type="submit" :class="btnPrimary" :disabled="saving">
              {{ saving ? 'Saving...' : isEdit ? 'Save Changes' : 'Create Match' }}
            </button>
            <RouterLink to="/matches" :class="btnOutline">Cancel</RouterLink>
          </div>
        </form>

        <div v-if="isEdit" class="mt-6 border-t border-red-100 pt-5">
          <button type="button" :class="btnDanger" @click="confirmingDelete = true">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
            </svg>
            Delete Match
          </button>
        </div>
      </section>
    </AsyncBlock>

    <ConfirmDialog
      :open="confirmingDelete"
      title="Delete this match?"
      message="This permanently deletes the match. All referee assignments will also be removed."
      confirm-text="Delete match"
      :busy="deleting"
      @confirm="confirmDelete"
      @cancel="confirmingDelete = false"
    />
  </PageShell>
</template>
