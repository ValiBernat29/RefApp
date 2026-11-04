import { reactive } from 'vue';
import { http } from './api/http.js';

/** Who is signed in. Loaded once at startup; the router waits for it. */
export const session = reactive({
  user: null,
  loaded: false,
  error: '',
});

export async function loadSession() {
  if (session.loaded) return;
  session.error = '';
  try {
    session.user = await http.get('/api/me');
    session.loaded = true;
  } catch (e) {
    session.error = e.message || 'Could not load your session.';
  }
}

export const hasRole = (role) => !!session.user?.roles?.includes(role);
