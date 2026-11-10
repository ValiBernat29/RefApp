import { http } from './http.js';

/**
 * Matches in a date range, optionally limited to one league.
 * @param {{league?: string, startDate?: string, endDate?: string}} filter
 */
export function listMatches({ league, startDate, endDate } = {}) {
  const params = new URLSearchParams();
  if (league) params.set('league', league);
  if (startDate) params.set('startDate', startDate);
  if (endDate) params.set('endDate', endDate);
  const query = params.toString();
  return http.get(`/api/board/matches${query ? `?${query}` : ''}`);
}

export const getMatch = (id) => http.get(`/api/board/matches/${id}`);

/** @param {{matchDate: string, homeTeamId: number, awayTeamId: number}} match */
export const createMatch = (match) => http.post('/api/board/matches', match);
export const updateMatch = (id, match) => http.put(`/api/board/matches/${id}`, match);

/** @returns {Promise<{name: string}>} */
export const deleteMatch = (id) => http.delete(`/api/board/matches/${id}`);
