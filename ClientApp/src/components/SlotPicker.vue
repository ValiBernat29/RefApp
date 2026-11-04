<script setup>
import { computed, ref } from 'vue';

const props = defineProps({
  label: { type: String, required: true },
  /** Candidates, already sorted for this slot by the server. */
  options: { type: Array, required: true },
  modelValue: { type: String, default: null },
  /** Referee id -> label of the other slot they already fill. */
  takenBy: { type: Object, default: () => ({}) },
});
const emit = defineEmits(['update:modelValue']);

const search = ref('');

const selected = computed(() => props.options.find((o) => o.id === props.modelValue) ?? null);

const visible = computed(() => {
  const q = search.value.trim().toLowerCase();
  return q ? props.options.filter((o) => o.name.toLowerCase().includes(q)) : props.options;
});

/** Why a referee cannot be picked, or '' if they can. Warnings (unavailable etc.) do not block. */
function blockReason(o) {
  if (props.takenBy[o.id]) return `Already ${props.takenBy[o.id]}`;
  if (o.hasCityConflict) return 'Home city conflict';
  if (o.hasConflictingMatch) return 'Time conflict';
  return '';
}

function badges(o) {
  const list = [];
  if (o.roleMatch) list.push({ text: 'Role match', tone: 'good' });
  if (o.distanceKm != null) list.push({ text: `${o.distanceKm} km`, tone: 'info' });
  if (o.hasCar) list.push({ text: 'Car', tone: 'good' });
  if (o.isUnavailable) list.push({ text: 'Unavailable', tone: 'bad' });
  if (o.isRefused) list.push({ text: 'Refused by team', tone: 'bad' });
  if (o.hasOtherMatchThatDay && !o.hasConflictingMatch) list.push({ text: 'Other match today', tone: 'warn' });
  if (o.recentMatchCount > 0) list.push({ text: `${o.recentMatchCount} recent`, tone: 'warn' });
  return list;
}

const TONES = {
  good: 'bg-emerald-50 text-emerald-700',
  info: 'bg-sky-50 text-sky-700',
  warn: 'bg-amber-50 text-amber-700',
  bad: 'bg-red-50 text-red-700',
};

function pick(o) {
  if (blockReason(o)) return;
  emit('update:modelValue', o.id === props.modelValue ? null : o.id);
}
</script>

<template>
  <div class="rounded-xl bg-white p-5 card-shadow">
    <div class="mb-3 flex items-start justify-between gap-3">
      <div class="min-w-0">
        <h2 class="text-sm font-semibold text-gray-700">{{ label }}</h2>
        <p v-if="selected" class="mt-0.5 truncate text-base font-semibold text-gray-900">{{ selected.name }}</p>
        <p v-else class="mt-0.5 text-sm italic text-gray-400">Not assigned</p>
      </div>
      <button
        v-if="selected"
        type="button"
        class="shrink-0 rounded-lg border border-gray-200 px-2.5 py-1 text-xs font-medium text-gray-500 hover:border-red-300 hover:text-red-600"
        @click="emit('update:modelValue', null)"
      >
        Clear
      </button>
    </div>

    <input
      v-model="search"
      type="search"
      placeholder="Search referees..."
      class="mb-2 w-full rounded-lg border-gray-200 px-3 py-2 text-sm focus:border-sky-400 focus:ring-sky-400"
      :aria-label="`Search ${label}`"
    />

    <ul class="max-h-80 space-y-1 overflow-y-auto pr-1" role="listbox" :aria-label="label">
      <li
        v-for="o in visible"
        :key="o.id"
        role="option"
        :aria-selected="o.id === modelValue"
        :aria-disabled="!!blockReason(o)"
        class="rounded-lg border px-3 py-2 transition-colors"
        :class="
          blockReason(o)
            ? 'cursor-not-allowed border-transparent bg-gray-50 opacity-60'
            : o.id === modelValue
              ? 'cursor-pointer border-sky-400 bg-sky-50'
              : 'cursor-pointer border-transparent hover:bg-gray-50'
        "
        @click="pick(o)"
      >
        <div class="flex items-center justify-between gap-2">
          <span class="truncate text-sm font-medium text-gray-800">{{ o.name }}</span>
          <span v-if="blockReason(o)" class="shrink-0 text-xs font-semibold text-red-600">{{ blockReason(o) }}</span>
        </div>
        <div v-if="badges(o).length" class="mt-1 flex flex-wrap gap-1">
          <span
            v-for="b in badges(o)"
            :key="b.text"
            class="rounded-full px-2 py-0.5 text-[11px] font-medium"
            :class="TONES[b.tone]"
          >
            {{ b.text }}
          </span>
        </div>
      </li>
      <li v-if="!visible.length" class="px-3 py-4 text-center text-sm text-gray-400">No referees match "{{ search }}"</li>
    </ul>
  </div>
</template>
