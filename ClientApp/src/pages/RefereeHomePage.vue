<script setup>
import { card } from '../components/ui/classes.js';

const actionClass =
  'group flex w-full items-center space-x-4 rounded-xl border border-[#91b5bf] px-4 py-4 text-left text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]';
const iconClass = 'rounded-md border border-gray-100 bg-gray-50 p-1 group-hover:bg-white';
</script>

<template>
  <main class="mx-auto grid max-w-[1440px] grid-cols-12 gap-8 p-8">
    <div class="col-span-12 space-y-8 lg:col-span-9">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">My Workspace</h1>
        <nav class="mt-0.5 text-sm text-gray-400" aria-label="Breadcrumb">
          <span>Dashboard</span><span class="mx-1">&bull;</span><span>Referee</span>
        </nav>
      </div>

      <div :class="[card, 'border-l-4 border-sky-400 p-6']">
        <p class="text-sm leading-relaxed text-gray-600">
          Welcome to your referee workspace. Use the quick actions to view your upcoming match assignments or manage your
          availability dates. Keep your unavailability up to date so the board can assign you to the right matches.
        </p>
      </div>
    </div>

    <aside class="col-span-12 lg:col-span-3">
      <section :class="[card, 'p-6']">
        <h2 class="mb-6 text-xl font-bold text-gray-800">Quick Actions</h2>
        <div class="flex flex-col space-y-4">
          <RouterLink to="/referee/matches" :class="actionClass">
            <div :class="iconClass">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
            </div>
            <span class="font-medium text-gray-600">My Matches</span>
          </RouterLink>

          <RouterLink to="/referee/availability" :class="actionClass">
            <div :class="iconClass">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" />
              </svg>
            </div>
            <span class="font-medium text-gray-600">My Availability</span>
          </RouterLink>
        </div>
      </section>
    </aside>
  </main>
</template>
