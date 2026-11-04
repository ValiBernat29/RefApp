import { http } from './http.js';

/** @returns {Promise<{referees: object[], matches: object[]}>} */
export const fetchRouteMapData = () => http.get('/api/board/route-map');
