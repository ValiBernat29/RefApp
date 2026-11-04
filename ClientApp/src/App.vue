<script setup>
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import AppHeader from './layout/AppHeader.vue';
import FlashMessage from './components/ui/FlashMessage.vue';
import { session, loadSession } from './session.js';

const route = useRoute();
// Full-window pages (the route map) bring their own chrome.
const bare = computed(() => !!route.meta.bare);
</script>

<template>
  <!-- Could not load who is signed in -->
  <div v-if="session.error" class="flex min-h-screen items-center justify-center bg-[#f4f7f9] p-6">
    <div class="max-w-md rounded-xl bg-white p-6 text-center card-shadow" role="alert">
      <p class="font-semibold text-gray-800">Could not load the app</p>
      <p class="mt-1 text-sm text-gray-500">{{ session.error }}</p>
      <button
        type="button"
        class="mt-4 rounded-lg bg-[#6eb7e4] px-5 py-2 text-sm font-medium text-white hover:bg-[#5ca5d2]"
        @click="loadSession()"
      >
        Try again
      </button>
    </div>
  </div>

  <div v-else-if="!session.loaded" class="flex min-h-screen items-center justify-center bg-[#f4f7f9]">
    <div class="h-8 w-8 animate-spin rounded-full border-[3px] border-gray-300 border-t-sky-500" role="status" aria-label="Loading"></div>
  </div>

  <RouterView v-else-if="bare" />

  <div v-else class="min-h-screen bg-[#f4f7f9] text-slate-800">
    <AppHeader />
    <FlashMessage />
    <RouterView />
  </div>
</template>
