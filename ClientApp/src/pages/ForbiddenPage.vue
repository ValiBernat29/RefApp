<script setup>
import PageShell from '../components/ui/PageShell.vue';
import { homePath } from '../router.js';
</script>

<template>
  <PageShell title="Access denied">
    <p class="text-gray-600">Your account does not have permission to view this page.</p>
    <RouterLink :to="homePath()" class="inline-block rounded-lg bg-[#6eb7e4] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#5ca5d2]">Back to my dashboard</RouterLink>
  </PageShell>
</template>
