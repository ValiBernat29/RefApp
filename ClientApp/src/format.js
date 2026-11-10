// Date formatting that matches what the Razor pages showed, using the date as stored (no time zone conversion).

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const pad = (n) => String(n).padStart(2, '0');
const toDate = (value) => (value instanceof Date ? value : new Date(value));

/** 07 Nov 2026 12:00 */
export function formatDateTime(value) {
  const d = toDate(value);
  return `${pad(d.getDate())} ${MONTHS[d.getMonth()]} ${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Sat, Nov 7, 2026 · 12:00 */
export function formatLongDateTime(value) {
  const d = toDate(value);
  return `${DAYS[d.getDay()]}, ${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()} \u00B7 ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** 07 Nov 2026 */
export function formatDate(value) {
  const d = toDate(value);
  return `${pad(d.getDate())} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
}

/** 2026-11-07 */
export function formatIsoDate(value) {
  const d = toDate(value);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Nov 7 */
export function formatMonthDay(value) {
  const d = toDate(value);
  return `${MONTHS[d.getMonth()]} ${d.getDate()}`;
}

/** Nov 7, 2026 */
export function formatMonthDayYear(value) {
  const d = toDate(value);
  return `${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}`;
}

/** Value for <input type="datetime-local">, rounded down to the hour. */
export function toLocalInputValue(value, snapToHour = true) {
  const d = toDate(value);
  if (snapToHour) d.setMinutes(0, 0, 0);
  return `${formatIsoDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** "2026-11-07T12:00" -> "2026-11-07T12:00:00", which the server's JSON parser requires. */
export const withSeconds = (localInput) => (localInput.length === 16 ? `${localInput}:00` : localInput);
