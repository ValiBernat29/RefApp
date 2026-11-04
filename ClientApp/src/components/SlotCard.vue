<script setup>
defineProps({
  label: { type: String, required: true },
  /** 'main' or 'asst', controls the dot colour. */
  kind: { type: String, required: true },
  referee: { type: Object, default: null },
  active: { type: Boolean, default: false },
});
defineEmits(['activate', 'clear']);

const roleBadge = (role) => (role === 'MainReferee' ? ' [Main]' : role === 'AssistantReferee' ? ' [Asst]' : '');
</script>

<template>
  <div
    class="mb-1.5 flex cursor-pointer items-center gap-2.5 rounded-[9px] border-[1.5px] bg-slate-900 px-[11px] py-[9px] transition"
    :class="active ? 'border-sky-500 bg-sky-500/10 ring-[3px] ring-sky-500/15' : 'border-slate-700 hover:border-slate-600'"
    @click="$emit('activate')"
  >
    <div class="h-2.5 w-2.5 shrink-0 rounded-full" :class="kind === 'main' ? 'bg-sky-500' : 'bg-violet-400'"></div>
    <div class="min-w-0 flex-1">
      <div class="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{{ label }}</div>
      <div v-if="referee" class="mt-px truncate text-[13px] font-semibold text-slate-200">
        {{ referee.name }}{{ referee.hasCar ? ' (Car)' : '' }}{{ roleBadge(referee.preferredRole) }}
      </div>
      <div v-else class="mt-px text-xs italic text-slate-600">Click to activate, then pick a ref</div>
    </div>
    <button
      v-if="referee"
      type="button"
      class="shrink-0 px-1 text-base leading-none text-slate-600 hover:text-red-400"
      aria-label="Clear slot"
      @click.stop="$emit('clear')"
    >
      &times;
    </button>
  </div>
</template>
