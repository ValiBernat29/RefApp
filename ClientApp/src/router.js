import { createRouter, createWebHistory } from 'vue-router';
import { loadSession, hasRole } from './session.js';
import { LEGACY } from './legacy.js';

const BOARD = ['Board'];

const routes = [
  // "/app" has no page of its own yet: send people to their dashboard (still a Razor page).
  { path: '/', name: 'home', component: { render: () => null } },

  { path: '/teams', name: 'teams', component: () => import('./pages/TeamsPage.vue'), meta: { roles: BOARD } },
  { path: '/teams/new', name: 'team-new', component: () => import('./pages/TeamFormPage.vue'), meta: { roles: BOARD } },
  {
    path: '/teams/:id(\\d+)/edit',
    name: 'team-edit',
    component: () => import('./pages/TeamFormPage.vue'),
    props: (route) => ({ id: Number(route.params.id) }),
    meta: { roles: BOARD },
  },

  {
    path: '/matches/:id(\\d+)/assign',
    name: 'assign',
    component: () => import('./pages/AssignPage.vue'),
    props: (route) => ({ matchId: Number(route.params.id) }),
    meta: { roles: BOARD },
  },

  // The map fills the whole window and has its own side panel, so it skips the normal header.
  { path: '/route-map', name: 'route-map', component: () => import('./pages/RouteMapPage.vue'), meta: { roles: BOARD, bare: true } },

  { path: '/forbidden', name: 'forbidden', component: () => import('./pages/ForbiddenPage.vue') },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('./pages/NotFoundPage.vue') },
];

export const router = createRouter({
  history: createWebHistory('/app/'),
  routes,
});

router.beforeEach(async (to) => {
  await loadSession();

  if (to.name === 'home') {
    location.assign(hasRole('Board') ? LEGACY.dashboard : LEGACY.refereeHome);
    return false;
  }

  const required = to.meta.roles;
  if (required && !required.some(hasRole)) return { name: 'forbidden', replace: true };
});
