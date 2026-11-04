<script setup>
import { computed } from 'vue';

const props = defineProps({
  referee: { type: Object, required: true },
  onRoute: { type: Boolean, default: false },
  /** Distance from the route line, in km (string, e.g. "2.4"). */
  offRouteKm: { type: String, default: null },
  /** Short label of the slot this referee fills ("Main", "Asst1"...), or '' if unassigned. */
  assignedSlot: { type: String, default: '' },
  slotActive: { type: Boolean, default: false },
  highlighted: { type: Boolean, default: false },
});
defineEmits(['select', 'assign']);

const hasCoords = computed(() => props.referee.lat != null && props.referee.lon != null);
const initial = computed(() => (props.referee.name || '?')[0].toUpperCase());

const cardClass = computed(() => {
  if (!hasCoords.value) return 'pointer-events-none opacity-40 border-transparent';
  if (props.assignedSlot) return 'border-indigo-500/30 bg-indigo-500/10';
  if (props.onRoute) return 'border-emerald-500/25 bg-emerald-500/10';
  return 'border-transparent hover:bg-slate-900';
});
const avatarClass = computed(() => {
  if (props.assignedSlot) return 'bg-indigo-500/30 text-indigo-300';
  if (props.onRoute) return 'bg-emerald-500/25 text-emerald-400';
  return 'bg-slate-700 text-slate-400';
});
</script>

<template>
  <div
    :data-id="referee.id"
    class="mb-[3px] flex cursor-pointer items-center gap-[9px] rounded-[9px] border-[1.5px] px-2.5 py-2 transition-colors"
    :class="[cardClass, highlighted ? 'outline outline-2 outline-sky-500' : '']"
    @click="$emit('select')"
  >
    <div class="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-full text-xs font-bold" :class="avatarClass">
      {{ initial }}
    </div>
    <div class="min-w-0 flex-1">
      <div class="truncate text-[13px] font-semibold text-slate-200">{{ referee.name }}</div>
      <div class="mt-px text-[11px] text-slate-500">
        {{ referee.city }}<template v-if="!hasCoords"> &middot; <i>no coords</i></template>
      </div>
      <div class="mt-[3px] flex flex-wrap gap-[3px]">
        <span v-if="referee.hasCar" class="rounded-full bg-emerald-500/15 px-1.5 py-px text-[10px] font-semibold text-emerald-400">Car</span>
        <span v-if="referee.preferredRole === 'MainReferee'" class="rounded-full bg-sky-500/15 px-1.5 py-px text-[10px] font-semibold text-sky-400">Main</span>
        <span v-else-if="referee.preferredRole === 'AssistantReferee'" class="rounded-full bg-purple-500/15 px-1.5 py-px text-[10px] font-semibold text-purple-400">Asst</span>
        <span v-if="onRoute && offRouteKm != null" class="rounded-full bg-amber-400/10 px-1.5 py-px text-[10px] font-semibold text-amber-400">{{ offRouteKm }}km off route</span>
        <span v-if="assignedSlot" class="rounded-full bg-indigo-500/20 px-1.5 py-px text-[10px] font-semibold text-indigo-300">{{ assignedSlot }}</span>
      </div>
    </div>
    <button
      v-if="slotActive"
      type="button"
      class="shrink-0 whitespace-nowrap rounded-md border border-slate-700 px-[7px] py-[3px] text-[11px] text-slate-500 transition hover:border-sky-500 hover:text-sky-500"
      @click.stop="$emit('assign')"
    >
      + Assign
    </button>
  </div>
</template>
