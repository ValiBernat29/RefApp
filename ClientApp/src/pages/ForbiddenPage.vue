<script setup>
import PageShell from '../components/ui/PageShell.vue';
import { hasRole } from '../session.js';
import { LEGACY } from '../legacy.js';
</script>

<template>
  <PageShell title="Access denied">
    <p class="text-gray-600">Your account does not have permission to view this page.</p>
    <a
      :href="hasRole('Board') ? LEGACY.dashboard : LEGACY.refereeHome"
      class="inline-block rounded-lg bg-[#6eb7e4] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#5ca5d2]"
    >
      Back to my dashboard
    </a>
  </PageShell>
</template>
