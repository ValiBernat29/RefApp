import { http } from './http.js';

let cached = null;

/**
 * Static option lists for forms (enum names and valid cities). Fetched once per page load.
 * @returns {Promise<{leagues: string[], matchDays: string[], cities: string[]}>}
 */
export function getLookups() {
  cached ??= http.get('/api/lookups').catch((e) => {
    cached = null; // allow a retry after a failure
    throw e;
  });
  return cached;
}
