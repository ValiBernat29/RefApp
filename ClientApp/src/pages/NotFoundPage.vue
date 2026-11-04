<script setup>
import PageShell from '../components/ui/PageShell.vue';
import { LEGACY } from '../legacy.js';
</script>

<template>
  <PageShell title="Page not found">
    <p class="text-gray-600">That page does not exist.</p>
    <a :href="LEGACY.dashboard" class="inline-block rounded-lg bg-[#6eb7e4] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#5ca5d2]">
      Go to the dashboard
    </a>
  </PageShell>
</template>
