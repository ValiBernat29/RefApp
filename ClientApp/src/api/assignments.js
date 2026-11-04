import { http } from './http.js';

/**
 * Match summary, current assignment and scored candidates per slot.
 * @returns {Promise<{match: object, current: object, candidates: {main: object[], assistant1: object[], assistant2: object[]}}>}
 */
export const fetchAssignment = (matchId) => http.get(`/api/board/matches/${matchId}/assignment`);

/** Validates and saves the three slots. Rejects with an ApiError carrying the server's reason. */
export const saveAssignment = (matchId, payload) => http.put(`/api/board/matches/${matchId}/assignment`, payload);
