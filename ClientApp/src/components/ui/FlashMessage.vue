<script setup>
import { flash, clearFlash } from '../../flash.js';
</script>

<template>
  <div v-if="flash.text" class="mx-auto max-w-[1440px] px-8 pt-6">
    <div
      class="flex items-start justify-between gap-4 rounded-lg border px-4 py-3 text-[13.5px]"
      :class="
        flash.kind === 'error'
          ? 'border-rose-600/20 bg-rose-100 text-rose-600'
          : 'border-emerald-600/20 bg-emerald-100 text-emerald-600'
      "
      :role="flash.kind === 'error' ? 'alert' : 'status'"
    >
      <span>{{ flash.text }}</span>
      <button type="button" class="shrink-0 leading-none opacity-60 hover:opacity-100" aria-label="Dismiss" @click="clearFlash">
        &times;
      </button>
    </div>
  </div>
</template>
