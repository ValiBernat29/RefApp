<script setup>
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { session, hasRole } from '../session.js';
import { LEGACY } from '../legacy.js';

const route = useRoute();

// `to` = page inside the Vue app (client-side navigation). `href` = Razor page (full page load).
const boardLinks = [
  { text: 'Home', href: LEGACY.dashboard },
  { text: 'Matches', href: LEGACY.matches },
  { text: 'Unavailability', href: LEGACY.unavailability },
  { text: 'Users', href: LEGACY.users },
  { text: 'Teams', to: '/teams' },
  { text: 'Auto-Allocate', href: LEGACY.allocation },
  { text: '\u{1F5FA} Route Map', to: '/route-map' },
];
const refereeLinks = [
  { text: 'Dashboard', href: LEGACY.refereeHome },
  { text: 'My Matches', href: LEGACY.refereeMatches },
  { text: 'My Availability', href: LEGACY.refereeAvailability },
];

const links = computed(() => (hasRole('Board') ? boardLinks : refereeLinks));

const isActive = (link) => !!link.to && (route.path === link.to || route.path.startsWith(link.to + '/'));

const baseClass = 'rounded-lg px-3 py-2 text-sm font-medium transition-colors';
const linkClass = (link) =>
  [baseClass, isActive(link) ? 'bg-sky-50 text-sky-700' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-800'];

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

// Served as a static file from wwwroot. A variable keeps Vite from trying to bundle it.
const LOGO_URL = '/images/RefappLogo.png';
</script>

<template>
  <header class="sticky top-0 z-50 flex items-center justify-between border-b border-gray-100 bg-white px-8 py-4">
    <div class="flex items-center space-x-8">
      <a href="/" class="flex items-center space-x-2" aria-label="RefApp home">
        <img :src="LOGO_URL" alt="RefApp Logo" class="h-24 w-auto object-contain" />
      </a>

      <nav class="hidden items-center space-x-1 md:flex" aria-label="Main">
        <template v-for="link in links" :key="link.text">
          <RouterLink v-if="link.to" :to="link.to" :class="linkClass(link)">{{ link.text }}</RouterLink>
          <a v-else :href="link.href" :class="linkClass(link)">{{ link.text }}</a>
        </template>
      </nav>
    </div>

    <div class="flex items-center space-x-3 border-l border-gray-100 pl-4">
      <div class="flex h-9 w-9 select-none items-center justify-center rounded-full bg-sky-400 text-xs font-semibold text-white">
        {{ initials }}
      </div>
      <a :href="LEGACY.account" class="text-sm font-medium text-gray-700 transition-colors hover:text-gray-900">
        {{ displayName }}
      </a>
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
