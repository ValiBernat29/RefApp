// Thin wrapper around the existing BoardController JSON endpoints.

function antiForgeryToken() {
  return document.querySelector('meta[name="af-token"]')?.content ?? '';
}

/** @returns {Promise<{referees: object[], matches: object[]}>} */
export async function fetchMapData() {
  const res = await fetch('/RouteMap/Data', { credentials: 'same-origin' });
  if (!res.ok) throw new Error(`Failed to load map data (${res.status})`);
  return res.json();
}

/**
 * Saves the three referee slots for one match.
 * @returns {Promise<{ok: boolean, error?: string}>}
 */
export async function saveMapAssignment(payload) {
  try {
    const res = await fetch('/RouteMap/Save', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        RequestVerificationToken: antiForgeryToken(),
      },
      body: JSON.stringify(payload),
    });
    const data = await res.json().catch(() => ({}));
    return res.ok ? { ok: true } : { ok: false, error: data.error || 'Save failed.' };
  } catch {
    return { ok: false, error: 'Network error.' };
  }
}

// ── Assign page ─────────────────────────────────────────────────────────────

/**
 * Match summary, current assignment and scored candidates per slot.
 * @returns {Promise<{match: object, current: object, candidates: {main: object[], assistant1: object[], assistant2: object[]}}>}
 */
export async function fetchAssignment(matchId) {
  const res = await fetch(`/api/board/matches/${matchId}/assignment`, { credentials: 'same-origin' });
  if (res.status === 404) throw new Error('Match not found.');
  if (!res.ok) throw new Error(`Failed to load assignment data (${res.status})`);
  return res.json();
}

/** @returns {Promise<{ok: boolean, error?: string}>} */
export async function saveAssignment(matchId, payload) {
  try {
    const res = await fetch(`/api/board/matches/${matchId}/assignment`, {
      method: 'PUT',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        RequestVerificationToken: antiForgeryToken(),
      },
      body: JSON.stringify(payload),
    });
    const data = await res.json().catch(() => ({}));
    return res.ok ? { ok: true } : { ok: false, error: data.error || 'Save failed.' };
  } catch {
    return { ok: false, error: 'Network error.' };
  }
}
