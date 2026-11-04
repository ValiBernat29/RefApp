<script setup>
defineProps({
  title: { type: String, required: true },
  /** [{ text, to? , href? }]: `to` is a Vue route, `href` a Razor page, none = current page. */
  breadcrumbs: { type: Array, default: () => [] },
});
</script>

<template>
  <main class="mx-auto max-w-[1440px] space-y-6 p-8">
    <div class="flex items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">{{ title }}</h1>
        <nav v-if="breadcrumbs.length" class="mt-0.5 text-sm text-gray-400" aria-label="Breadcrumb">
          <template v-for="(crumb, i) in breadcrumbs" :key="crumb.text">
            <span v-if="i > 0" class="mx-1">&bull;</span>
            <RouterLink v-if="crumb.to" :to="crumb.to" class="hover:text-gray-600">{{ crumb.text }}</RouterLink>
            <a v-else-if="crumb.href" :href="crumb.href" class="hover:text-gray-600">{{ crumb.text }}</a>
            <span v-else>{{ crumb.text }}</span>
          </template>
        </nav>
      </div>
      <div v-if="$slots.actions"><slot name="actions" /></div>
    </div>

    <slot />
  </main>
</template>
