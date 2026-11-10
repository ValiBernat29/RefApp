import { http } from './http.js';

export const listUsers = () => http.get('/api/board/users');
export const getUser = (id) => http.get(`/api/board/users/${id}`);
export const updateUser = (id, user) => http.put(`/api/board/users/${id}`, user);
export const deleteUser = (id) => http.delete(`/api/board/users/${id}`);

/** @returns {Promise<{hasCar: boolean}>} the new value */
export const toggleCar = (id) => http.post(`/api/board/users/${id}/toggle-car`);
