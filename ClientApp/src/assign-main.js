import { createApp } from 'vue';
import './style.css';
import AssignPage from './AssignPage.vue';

const root = document.getElementById('assign-app');
createApp(AssignPage, { matchId: Number(root.dataset.matchId) }).mount(root);
