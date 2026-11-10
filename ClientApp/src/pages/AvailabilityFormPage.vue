<script setup>
import { ref, reactive, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import PageShell from '../components/ui/PageShell.vue';
import AsyncBlock from '../components/ui/AsyncBlock.vue';
import FormField from '../components/ui/FormField.vue';
import { getMyUnavailability, createMyUnavailability, updateMyUnavailability } from '../api/referee.js';
import { showFlash } from '../flash.js';
import { formatIsoDate } from '../format.js';
import { btnPrimary, btnOutline, card, inputClass } from '../components/ui/classes.js';

// Present when editing (/referee/availability/:id/edit), absent when adding.
const props = defineProps({ id: { type: Number, default: null } });

const router = useRouter();
const isEdit = computed(() => props.id !== null);

const today = formatIsoDate(new Date());
const form = reactive({ startDate: today, endDate: today, reason: '' });
const errors = ref({});
const formError = ref('');
const loading = ref(isEdit.value);
const loadError = ref('');
const saving = ref(false);

function validate() {
  const found = {};
  if (!form.startDate) found.startDate = 'Please choose a start date.';
  if (!form.endDate) found.endDate = 'Please choose an end date.';
  else if (form.startDate && form.endDate < form.startDate) found.endDate = 'End date must be on or after start date.';
  errors.value = found;
  return Object.keys(found).length === 0;
}

async function submit() {
  formError.value = '';
  if (!validate()) return;
  saving.value = true;
  const payload = { startDate: form.startDate, endDate: form.endDate, reason: form.reason.trim() || null };
  try {
    if (isEdit.value) await updateMyUnavailability(props.id, payload);
    else await createMyUnavailability(payload);
    showFlash(isEdit.value ? 'Unavailability updated.' : 'Unavailability added.');
    router.push('/referee/availability');
  } catch (e) {
    errors.value = e.fieldErrors ?? {};
    if (!Object.keys(errors.value).length) formError.value = e.message;
    saving.value = false;
  }
}

onMounted(async () => {
  if (!isEdit.value) return;
  try {
    const item = await getMyUnavailability(props.id);
    Object.assign(form, {
      startDate: formatIsoDate(item.startDate),
      endDate: formatIsoDate(item.endDate),
      reason: item.reason ?? '',
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
    :title="isEdit ? 'Edit Unavailability' : 'Add Unavailability'"
    :breadcrumbs="[
      { text: 'Dashboard', to: '/referee' },
      { text: 'My Availability', to: '/referee/availability' },
      { text: isEdit ? 'Edit' : 'Add' },
    ]"
  >
    <AsyncBlock :loading="loading" :error="loadError">
      <section :class="[card, 'max-w-xl p-6']">
        <form class="space-y-5" novalidate @submit.prevent="submit">
          <p v-if="formError" class="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-600" role="alert">
            {{ formError }}
          </p>

          <FormField label="Start Date" for="start-date" :error="errors.startDate">
            <input id="start-date" v-model="form.startDate" type="date" :class="inputClass(!!errors.startDate)" />
          </FormField>

          <FormField label="End Date" for="end-date" :error="errors.endDate">
            <input id="end-date" v-model="form.endDate" type="date" :class="inputClass(!!errors.endDate)" />
          </FormField>

          <FormField label="Reason (optional)" for="reason" :error="errors.reason">
            <input id="reason" v-model="form.reason" type="text" maxlength="500" placeholder="e.g. Vacation, injury" :class="inputClass(!!errors.reason)" />
          </FormField>

          <div class="flex items-center gap-3 pt-2">
            <button type="submit" :class="btnPrimary" :disabled="saving">{{ saving ? 'Saving...' : isEdit ? 'Save' : 'Add' }}</button>
            <RouterLink to="/referee/availability" :class="btnOutline">Cancel</RouterLink>
          </div>
        </form>
      </section>
    </AsyncBlock>
  </PageShell>
</template>
