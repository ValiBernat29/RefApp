// One place that talks to the server. Pages call the typed functions in this folder and never use fetch directly.

/** Thrown for every failed call. `fieldErrors` maps form field names to a message when the server rejects input. */
export class ApiError extends Error {
  constructor(status, message, fieldErrors = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

const DEFAULT_MESSAGES = {
  400: 'The request was not valid.',
  403: 'You do not have permission to do that.',
  404: 'That item no longer exists.',
  409: 'That change conflicts with existing data.',
};

function antiForgeryToken() {
  return document.querySelector('meta[name="af-token"]')?.content ?? '';
}

function toFieldErrors(errors) {
  const result = {};
  for (const [key, messages] of Object.entries(errors ?? {})) {
    // ASP.NET reports "Name" or "$.league"; the forms use "name" and "league".
    const field = key.replace(/^\$\./, '');
    const name = field.charAt(0).toLowerCase() + field.slice(1);
    result[name] = Array.isArray(messages) ? messages[0] : String(messages);
  }
  return result;
}

function toApiError(status, body) {
  const fieldErrors = toFieldErrors(body?.errors);
  const firstField = Object.values(fieldErrors)[0];
  const message =
    body?.error ?? firstField ?? body?.title ?? DEFAULT_MESSAGES[status] ?? 'Something went wrong. Please try again.';
  return new ApiError(status, message, fieldErrors);
}

async function request(method, url, body) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (method !== 'GET') headers.RequestVerificationToken = antiForgeryToken();

  let res;
  try {
    res = await fetch(url, {
      method,
      credentials: 'same-origin',
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'Could not reach the server. Check your connection and try again.');
  }

  if (res.status === 401) {
    // Session expired: go to the (Razor) login page and come back here afterwards.
    const returnUrl = encodeURIComponent(location.pathname + location.search);
    location.assign(`/Identity/Account/Login?ReturnUrl=${returnUrl}`);
    throw new ApiError(401, 'Your session has expired. Redirecting to sign in...');
  }

  const data = res.status === 204 ? null : await res.json().catch(() => null);
  if (!res.ok) throw toApiError(res.status, data);
  return data;
}

export const http = {
  get: (url) => request('GET', url),
  post: (url, body) => request('POST', url, body ?? {}),
  put: (url, body) => request('PUT', url, body ?? {}),
  delete: (url) => request('DELETE', url),
};
