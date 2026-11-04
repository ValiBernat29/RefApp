import { http } from './http.js';

/** @typedef {{id: number, name: string, league: string, preferredMatchDay: string, city: string|null, hasCoordinates: boolean}} Team */

/** @returns {Promise<Team[]>} */
export const listTeams = () => http.get('/api/board/teams');

/** @returns {Promise<Team>} */
export const getTeam = (id) => http.get(`/api/board/teams/${id}`);

/** @param {{name: string, league: string, preferredMatchDay: string, city: string|null}} team */
export const createTeam = (team) => http.post('/api/board/teams', team);

export const updateTeam = (id, team) => http.put(`/api/board/teams/${id}`, team);

/** @returns {Promise<{deletedMatches: number}>} */
export const deleteTeam = (id) => http.delete(`/api/board/teams/${id}`);
