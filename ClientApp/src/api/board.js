import { http } from './http.js';

export const getDashboard = () => http.get('/api/board/dashboard');
export const listUnavailability = () => http.get('/api/board/unavailability');

/**
 * Runs the allocation solver and saves its result. Overwrites assignments in the range.
 * @param {{startDate: string, endDate: string}} range
 */
export const runAllocation = (range) => http.post('/api/board/allocation', range);
