<script setup>
// Label + control + error message. The control goes in the default slot and should use the same `id`.
defineProps({
  label: { type: String, required: true },
  for: { type: String, required: true },
  error: { type: String, default: '' },
  hint: { type: String, default: '' },
});
</script>

<template>
  <div class="space-y-1.5">
    <label :for="$props.for" class="block text-sm font-medium text-gray-700">{{ label }}</label>
    <slot />
    <p v-if="hint && !error" class="text-xs text-gray-400">{{ hint }}</p>
    <p v-if="error" class="text-xs text-rose-600">{{ error }}</p>
  </div>
</template>
