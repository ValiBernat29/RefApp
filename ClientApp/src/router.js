import { createRouter, createWebHistory } from 'vue-router';
import { loadSession, hasRole } from './session.js';

const BOARD = ['Board'];
const REFEREE = ['Referee'];

const idParam = (route) => ({ id: Number(route.params.id) });

const routes = [
  // "/app" itself has no page: the guard below sends people to their own dashboard.
  { path: '/', name: 'home', component: { render: () => null } },

  // ── Board ────────────────────────────────────────────────────────────────
  { path: '/dashboard', component: () => import('./pages/DashboardPage.vue'), meta: { roles: BOARD } },

  { path: '/matches', component: () => import('./pages/MatchesPage.vue'), meta: { roles: BOARD } },
  { path: '/matches/new', component: () => import('./pages/MatchFormPage.vue'), meta: { roles: BOARD } },
  { path: '/matches/:id(\\d+)/edit', component: () => import('./pages/MatchFormPage.vue'), props: idParam, meta: { roles: BOARD } },
  {
    path: '/matches/:id(\\d+)/assign',
    component: () => import('./pages/AssignPage.vue'),
    props: (route) => ({ matchId: Number(route.params.id) }),
    meta: { roles: BOARD },
  },

  { path: '/teams', component: () => import('./pages/TeamsPage.vue'), meta: { roles: BOARD } },
  { path: '/teams/new', component: () => import('./pages/TeamFormPage.vue'), meta: { roles: BOARD } },
  { path: '/teams/:id(\\d+)/edit', component: () => import('./pages/TeamFormPage.vue'), props: idParam, meta: { roles: BOARD } },

  { path: '/users', component: () => import('./pages/UsersPage.vue'), meta: { roles: BOARD } },
  { path: '/users/fleet', component: () => import('./pages/FleetPage.vue'), meta: { roles: BOARD } },
  {
    path: '/users/:id/edit',
    component: () => import('./pages/UserEditPage.vue'),
    props: (route) => ({ id: route.params.id }), // user ids are GUID strings
    meta: { roles: BOARD },
  },

  { path: '/unavailability', component: () => import('./pages/UnavailabilityPage.vue'), meta: { roles: BOARD } },
  { path: '/allocation', component: () => import('./pages/AllocationPage.vue'), meta: { roles: BOARD } },

  // The map fills the whole window and has its own side panel, so it skips the normal header.
  { path: '/route-map', component: () => import('./pages/RouteMapPage.vue'), meta: { roles: BOARD, bare: true } },

  // ── Referee ──────────────────────────────────────────────────────────────
  { path: '/referee', component: () => import('./pages/RefereeHomePage.vue'), meta: { roles: REFEREE } },
  { path: '/referee/matches', component: () => import('./pages/RefereeMatchesPage.vue'), meta: { roles: REFEREE } },
  { path: '/referee/availability', component: () => import('./pages/RefereeAvailabilityPage.vue'), meta: { roles: REFEREE } },
  { path: '/referee/availability/new', component: () => import('./pages/AvailabilityFormPage.vue'), meta: { roles: REFEREE } },
  {
    path: '/referee/availability/:id(\\d+)/edit',
    component: () => import('./pages/AvailabilityFormPage.vue'),
    props: idParam,
    meta: { roles: REFEREE },
  },

  { path: '/forbidden', name: 'forbidden', component: () => import('./pages/ForbiddenPage.vue') },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('./pages/NotFoundPage.vue') },
];

export const router = createRouter({
  history: createWebHistory('/app/'),
  routes,
  scrollBehavior: () => ({ top: 0 }),
});

/** Where each kind of user starts. */
export const homePath = () => (hasRole('Board') ? '/dashboard' : '/referee');

router.beforeEach(async (to) => {
  await loadSession();

  if (to.name === 'home') return homePath();

  const required = to.meta.roles;
  if (required && !required.some(hasRole)) return { name: 'forbidden', replace: true };
});
