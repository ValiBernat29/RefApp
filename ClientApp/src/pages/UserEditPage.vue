<script setup>
import { ref, reactive, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import FormField from '../components/ui/FormField.vue';
import { getUser, updateUser } from '../api/users.js';
import { getLookups } from '../api/lookups.js';
import { showFlash } from '../flash.js';
import { btnPrimary, btnOutline, card, inputClass } from '../components/ui/classes.js';

const props = defineProps({ id: { type: String, required: true } });

const router = useRouter();
const lookups = ref({ ranks: [], preferredRoles: [], cities: [] });
const form = reactive({
  userName: '',
  displayName: '',
  role: 'Referee',
  newPassword: '',
  homeCity: '',
  rank: 'None',
  preferredRole: 'None',
  hasCar: false,
});
const errors = ref({});
const formError = ref('');
const loading = ref(true);
const loadError = ref('');
const saving = ref(false);

function validate() {
  const found = {};
  if (!form.userName.trim()) found.userName = 'Please enter a username.';
  if (form.homeCity.trim() && !lookups.value.cities.includes(form.homeCity.trim())) {
    found.homeCity = 'Please select a valid city or village from the list.';
  }
  errors.value = found;
  return Object.keys(found).length === 0;
}

async function submit() {
  formError.value = '';
  if (!validate()) return;
  saving.value = true;
  try {
    await updateUser(props.id, {
      userName: form.userName.trim(),
      displayName: form.displayName.trim(),
      role: form.role,
      newPassword: form.newPassword || null,
      homeCity: form.homeCity.trim() || null,
      rank: form.rank,
      preferredRole: form.preferredRole,
      hasCar: form.hasCar,
    });
    showFlash('Account updated successfully.');
    router.push('/users');
  } catch (e) {
    errors.value = e.fieldErrors ?? {};
    if (!Object.keys(errors.value).length) formError.value = e.message;
    saving.value = false;
  }
}

onMounted(async () => {
  try {
    const [options, user] = await Promise.all([getLookups(), getUser(props.id)]);
    lookups.value = options;
    Object.assign(form, {
      userName: user.userName ?? '',
      displayName: user.displayName ?? '',
      role: user.role,
      homeCity: user.homeCity ?? '',
      rank: user.rank,
      preferredRole: user.preferredRole,
      hasCar: user.hasCar,
    });
  } catch (e) {
    loadError.value = e.message;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <PageShell
    title="Edit Account"
    :breadcrumbs="[{ text: 'Dashboard', to: '/dashboard' }, { text: 'Users', to: '/users' }, { text: 'Edit' }]"
  >
    <AsyncBlock :loading="loading" :error="loadError">
      <section :class="[card, 'max-w-xl p-6']">
        <form class="space-y-5" novalidate @submit.prevent="submit">
          <p v-if="formError" class="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-600" role="alert">
            {{ formError }}
          </p>

          <FormField label="Username" for="user-name" :error="errors.userName">
            <input id="user-name" v-model="form.userName" type="text" :class="inputClass(!!errors.userName)" />
          </FormField>

          <FormField label="Display Name (Optional)" for="user-display" :error="errors.displayName">
            <input id="user-display" v-model="form.displayName" type="text" placeholder="e.g. John Doe" :class="inputClass()" />
          </FormField>

          <FormField label="Role" for="user-role" :error="errors.role">
            <select id="user-role" v-model="form.role" :class="inputClass()">
              <option value="Referee">Referee</option>
              <option value="Board">Board</option>
            </select>
          </FormField>

          <FormField
            label="New Password (Optional)"
            for="user-password"
            hint="Only fill this out if you want to force a password change."
            :error="errors.newPassword"
          >
            <input
              id="user-password"
              v-model="form.newPassword"
              type="password"
              autocomplete="new-password"
              placeholder="Leave blank to keep current password"
              :class="inputClass(!!errors.newPassword)"
            />
          </FormField>

          <FormField label="Home City / Village" for="user-city" :error="errors.homeCity">
            <input
              id="user-city"
              v-model="form.homeCity"
              type="text"
              list="user-localities"
              placeholder="Type to search..."
              autocomplete="off"
              :class="inputClass(!!errors.homeCity)"
            />
            <datalist id="user-localities">
              <option v-for="c in lookups.cities" :key="c" :value="c"></option>
            </datalist>
          </FormField>

          <FormField label="Referee Tier (internal)" for="user-rank">
            <select id="user-rank" v-model="form.rank" :class="inputClass()">
              <option v-for="r in lookups.ranks" :key="r" :value="r">{{ r }}</option>
            </select>
          </FormField>

          <FormField label="Preferred Role" for="user-preferred">
            <select id="user-preferred" v-model="form.preferredRole" :class="inputClass()">
              <option v-for="r in lookups.preferredRoles" :key="r" :value="r">{{ r }}</option>
            </select>
          </FormField>

          <div class="flex items-center justify-between rounded-lg border border-gray-200 bg-gray-50 p-4">
            <div>
              <p id="has-car-label" class="text-sm font-medium text-gray-700">Has a Car</p>
              <p class="mt-0.5 text-xs text-gray-400">Used by auto-allocation to ensure transport coverage per match.</p>
            </div>
            <button
              type="button"
              role="switch"
              :aria-checked="form.hasCar"
              aria-labelledby="has-car-label"
              class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-sky-300"
              :class="form.hasCar ? 'bg-emerald-500' : 'bg-gray-300'"
              @click="form.hasCar = !form.hasCar"
            >
              <span
                class="inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform"
                :class="form.hasCar ? 'translate-x-[22px]' : 'translate-x-0.5'"
              ></span>
            </button>
          </div>

          <div class="flex items-center gap-3 pt-2">
            <button type="submit" :class="btnPrimary" :disabled="saving">{{ saving ? 'Saving...' : 'Save Changes' }}</button>
            <RouterLink to="/users" :class="btnOutline">Cancel</RouterLink>
          </div>
        </form>
      </section>
    </AsyncBlock>
  </PageShell>
</template>
