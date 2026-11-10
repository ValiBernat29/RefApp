<script setup>
// Shows a spinner while loading, an error with a retry button on failure, and the page content otherwise.
defineProps({
  loading: { type: Boolean, default: false },
  error: { type: String, default: '' },
  loadingText: { type: String, default: 'Loading...' },
});
defineEmits(['retry']);
</script>

<template>
  <div v-if="loading" class="flex items-center gap-3 py-10 text-gray-500" role="status">
    <div class="h-5 w-5 animate-spin rounded-full border-2 border-gray-300 border-t-sky-500"></div>
    {{ loadingText }}
  </div>

  <div v-else-if="error" class="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700" role="alert">
    {{ error }}
    <button type="button" class="ml-2 font-medium underline" @click="$emit('retry')">Retry</button>
  </div>

  <slot v-else />
</template>
