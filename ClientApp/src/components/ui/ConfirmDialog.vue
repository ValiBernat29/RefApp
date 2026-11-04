<script setup>
import { ref, watch } from 'vue';

const props = defineProps({
  open: { type: Boolean, default: false },
  title: { type: String, required: true },
  message: { type: String, default: '' },
  confirmText: { type: String, default: 'Confirm' },
  busy: { type: Boolean, default: false },
});
const emit = defineEmits(['confirm', 'cancel']);

// The native <dialog> gives focus trapping, Escape-to-close and a backdrop for free.
const dialog = ref(null);

watch(
  () => props.open,
  (open) => {
    if (!dialog.value) return;
    if (open && !dialog.value.open) dialog.value.showModal();
    else if (!open && dialog.value.open) dialog.value.close();
  },
  { flush: 'post' },
);

// A click on the dialog element itself (not its content) is a click on the backdrop.
const onClick = (e) => {
  if (e.target === dialog.value && !props.busy) emit('cancel');
};
</script>

<template>
  <dialog
    ref="dialog"
    class="w-full max-w-md rounded-xl p-0 shadow-xl backdrop:bg-black/40"
    aria-labelledby="confirm-title"
    @cancel.prevent="!busy && emit('cancel')"
    @click="onClick"
  >
    <div class="p-6">
      <h2 id="confirm-title" class="text-lg font-semibold text-gray-900">{{ title }}</h2>
      <p v-if="message" class="mt-2 text-sm text-gray-600">{{ message }}</p>
      <div class="mt-6 flex justify-end gap-3">
        <button
          type="button"
          class="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50"
          :disabled="busy"
          @click="emit('cancel')"
        >
          Cancel
        </button>
        <button
          type="button"
          class="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
          :disabled="busy"
          @click="emit('confirm')"
        >
          {{ busy ? 'Working...' : confirmText }}
        </button>
      </div>
    </div>
  </dialog>
</template>
