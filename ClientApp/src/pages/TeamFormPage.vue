<script setup>
import { ref, reactive, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import PageShell from '../components/ui/PageShell.vue';
import { getTeam, createTeam, updateTeam } from '../api/teams.js';
import { getLookups } from '../api/lookups.js';
import { showFlash } from '../flash.js';
import { LEGACY } from '../legacy.js';

// Present when editing (/teams/:id/edit), absent when creating (/teams/new).
const props = defineProps({ id: { type: Number, default: null } });

const router = useRouter();
const isEdit = computed(() => props.id !== null);

const lookups = ref({ leagues: [], matchDays: [], cities: [] });
const form = reactive({ name: '', league: '', preferredMatchDay: 'Sunday', city: '' });
const errors = ref({}); // field name -> message
const formError = ref(''); // problem not tied to one field
const loading = ref(true);
const loadError = ref('');
const saving = ref(false);

const inputClass = (field) => [
  'w-full rounded-lg border px-3 py-2.5 text-sm text-gray-700 focus:outline-none focus:ring-2',
  errors.value[field]
    ? 'border-rose-400 focus:border-rose-400 focus:ring-rose-200'
    : 'border-gray-200 focus:border-sky-400 focus:ring-sky-300',
];

function validate() {
  const found = {};
  if (!form.name.trim()) found.name = 'Please enter a team name.';
  if (form.city.trim() && !lookups.value.cities.includes(form.city.trim())) {
    found.city = 'Please select a valid city or village from the list.';
  }
  errors.value = found;
  return Object.keys(found).length === 0;
}

async function submit() {
  formError.value = '';
  if (!validate()) return;

  saving.value = true;
  const payload = {
    name: form.name.trim(),
    league: form.league,
    preferredMatchDay: form.preferredMatchDay,
    city: form.city.trim() || null,
  };
  try {
    if (isEdit.value) await updateTeam(props.id, payload);
    else await createTeam(payload);
    showFlash(isEdit.value ? 'Team updated successfully.' : 'Team created successfully.');
    router.push('/teams');
  } catch (e) {
    errors.value = e.fieldErrors ?? {};
    if (!Object.keys(errors.value).length) formError.value = e.message;
    saving.value = false;
  }
}

onMounted(async () => {
  try {
    lookups.value = await getLookups();
    if (isEdit.value) {
      const team = await getTeam(props.id);
      Object.assign(form, {
        name: team.name,
        league: team.league,
        preferredMatchDay: team.preferredMatchDay,
        city: team.city ?? '',
      });
    } else {
      form.league = lookups.value.leagues[0] ?? '';
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
    :title="isEdit ? 'Edit Team' : 'Add New Team'"
    :breadcrumbs="[
      { text: 'Dashboard', href: LEGACY.dashboard },
      { text: 'Teams', to: '/teams' },
      { text: isEdit ? 'Edit' : 'New' },
    ]"
  >
    <div v-if="loading" class="flex items-center gap-3 py-10 text-gray-500">
      <div class="h-5 w-5 animate-spin rounded-full border-2 border-gray-300 border-t-sky-500"></div>
      Loading...
    </div>

    <div v-else-if="loadError" class="max-w-xl rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700" role="alert">
      {{ loadError }}
      <RouterLink to="/teams" class="ml-2 font-medium underline">Back to teams</RouterLink>
    </div>

    <section v-else class="max-w-xl rounded-xl bg-white p-6 card-shadow">
      <form class="space-y-5" novalidate @submit.prevent="submit">
        <p v-if="formError" class="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-600" role="alert">
          {{ formError }}
        </p>

        <div class="space-y-1.5">
          <label for="team-name" class="block text-sm font-medium text-gray-700">Team Name</label>
          <input id="team-name" v-model="form.name" type="text" maxlength="200" :class="inputClass('name')" :aria-invalid="!!errors.name" />
          <p v-if="errors.name" class="text-xs text-rose-600">{{ errors.name }}</p>
        </div>

        <div class="space-y-1.5">
          <label for="team-league" class="block text-sm font-medium text-gray-700">League</label>
          <select id="team-league" v-model="form.league" :class="inputClass('league')">
            <option v-for="l in lookups.leagues" :key="l" :value="l">{{ l }}</option>
          </select>
          <p v-if="errors.league" class="text-xs text-rose-600">{{ errors.league }}</p>
        </div>

        <div class="space-y-1.5">
          <label for="team-day" class="block text-sm font-medium text-gray-700">Preferred Match Day</label>
          <select id="team-day" v-model="form.preferredMatchDay" :class="inputClass('preferredMatchDay')">
            <option v-for="d in lookups.matchDays" :key="d" :value="d">{{ d }}</option>
          </select>
          <p v-if="errors.preferredMatchDay" class="text-xs text-rose-600">{{ errors.preferredMatchDay }}</p>
        </div>

        <div class="space-y-1.5">
          <label for="team-city" class="block text-sm font-medium text-gray-700">Ground City / Village</label>
          <input
            id="team-city"
            v-model="form.city"
            type="text"
            list="arad-localities"
            maxlength="100"
            placeholder="Type to search..."
            autocomplete="off"
            :class="inputClass('city')"
            :aria-invalid="!!errors.city"
          />
          <datalist id="arad-localities">
            <option v-for="c in lookups.cities" :key="c" :value="c"></option>
          </datalist>
          <p v-if="errors.city" class="text-xs text-rose-600">{{ errors.city }}</p>
        </div>

        <div class="flex items-center gap-3 pt-2">
          <button
            type="submit"
            class="rounded-lg bg-[#6eb7e4] px-6 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#5ca5d2] disabled:cursor-not-allowed disabled:opacity-50"
            :disabled="saving"
          >
            {{ saving ? 'Saving...' : isEdit ? 'Save Changes' : 'Create Team' }}
          </button>
          <RouterLink
            to="/teams"
            class="rounded-lg border border-[#91b5bf] px-6 py-2.5 text-sm font-medium text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]"
          >
            Cancel
          </RouterLink>
        </div>
      </form>
    </section>
  </PageShell>
</template>
