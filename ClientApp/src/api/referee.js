import { http } from './http.js';

export const listMyMatches = () => http.get('/api/referee/matches');

export const listMyUnavailability = () => http.get('/api/referee/unavailability');
export const getMyUnavailability = (id) => http.get(`/api/referee/unavailability/${id}`);

/** @param {{startDate: string, endDate: string, reason: string|null}} period */
export const createMyUnavailability = (period) => http.post('/api/referee/unavailability', period);
export const updateMyUnavailability = (id, period) => http.put(`/api/referee/unavailability/${id}`, period);
export const deleteMyUnavailability = (id) => http.delete(`/api/referee/unavailability/${id}`);
