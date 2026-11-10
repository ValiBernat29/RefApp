<script setup>
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { session, hasRole } from '../session.js';

const route = useRoute();

// Account management stays on the built-in ASP.NET Identity pages (a normal page load).
const ACCOUNT_URL = '/Identity/Account/Manage';
// Served as a static file from wwwroot. A variable keeps Vite from trying to bundle it.
const LOGO_URL = '/images/RefappLogo.png';

const boardLinks = [
  { text: 'Home', to: '/dashboard' },
  { text: 'Matches', to: '/matches' },
  { text: 'Unavailability', to: '/unavailability' },
  { text: 'Users', to: '/users' },
  { text: 'Teams', to: '/teams' },
  { text: 'Auto-Allocate', to: '/allocation' },
  { text: '\u{1F5FA} Route Map', to: '/route-map' },
];
const refereeLinks = [
  { text: 'Dashboard', to: '/referee', exact: true },
  { text: 'My Matches', to: '/referee/matches' },
  { text: 'My Availability', to: '/referee/availability' },
];

const links = computed(() => (hasRole('Board') ? boardLinks : refereeLinks));
const homeTo = computed(() => (hasRole('Board') ? '/dashboard' : '/referee'));

// The Fleet page is part of Users, so Users stays highlighted there.
const isActive = (link) =>
  link.exact ? route.path === link.to : route.path === link.to || route.path.startsWith(link.to + '/');

const linkClass = (link) => [
  'rounded-lg px-3 py-2 text-sm font-medium transition-colors',
  isActive(link) ? 'bg-sky-50 text-sky-700' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-800',
];

const displayName = computed(() => session.user?.displayName || 'Account');
const initials = computed(() =>
  displayName.value
    .split(' ')
    .filter(Boolean)
    .map((s) => s[0])
    .slice(0, 2)
    .join('')
    .toUpperCase(),
);

const antiForgeryToken = document.querySelector('meta[name="af-token"]')?.content ?? '';
</script>

<template>
  <header class="sticky top-0 z-50 flex items-center justify-between border-b border-gray-100 bg-white px-8 py-4">
    <div class="flex items-center space-x-8">
      <RouterLink :to="homeTo" class="flex items-center space-x-2" aria-label="RefApp home">
        <img :src="LOGO_URL" alt="RefApp Logo" class="h-24 w-auto object-contain" />
      </RouterLink>

      <nav class="hidden items-center space-x-1 md:flex" aria-label="Main">
        <RouterLink v-for="link in links" :key="link.to" :to="link.to" :class="linkClass(link)">{{ link.text }}</RouterLink>
      </nav>
    </div>

    <div class="flex items-center space-x-3 border-l border-gray-100 pl-4">
      <div class="flex h-9 w-9 select-none items-center justify-center rounded-full bg-sky-400 text-xs font-semibold text-white">
        {{ initials }}
      </div>
      <a :href="ACCOUNT_URL" class="text-sm font-medium text-gray-700 transition-colors hover:text-gray-900">{{ displayName }}</a>
      <!-- Sign-out stays an ASP.NET Identity page; this is the same POST the Razor header sends. -->
      <form method="post" action="/Identity/Account/Logout?returnUrl=%2F">
        <input type="hidden" name="__RequestVerificationToken" :value="antiForgeryToken" />
        <button
          type="submit"
          class="rounded-lg border border-gray-200 px-3 py-1.5 text-xs text-gray-400 transition-colors hover:border-red-300 hover:text-red-500"
        >
          Logout
        </button>
      </form>
    </div>
  </header>
</template>
