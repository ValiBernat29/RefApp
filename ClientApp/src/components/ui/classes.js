// Shared Tailwind class strings, so buttons, inputs and cards look the same on every page.

export const btnPrimary =
  'inline-flex items-center justify-center gap-2 rounded-lg bg-[#6eb7e4] px-6 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#5ca5d2] disabled:cursor-not-allowed disabled:opacity-50';

export const btnOutline =
  'inline-flex items-center justify-center rounded-lg border border-[#91b5bf] px-6 py-2.5 text-sm font-medium text-[#6193a0] transition-all hover:border-[#6193a0] hover:bg-[#f0f6f8]';

export const btnDanger =
  'inline-flex items-center justify-center gap-2 rounded-lg border border-red-200 bg-red-50 px-6 py-2.5 text-sm font-medium text-red-600 transition-colors hover:border-red-400 hover:bg-red-100 disabled:opacity-50';

// Smaller versions for table rows.
export const btnPrimarySm = btnPrimary.replace('px-6 py-2.5', 'px-4 py-1.5');
export const btnOutlineSm = btnOutline.replace('px-6 py-2.5', 'px-4 py-1.5');
export const btnDangerSm = btnDanger.replace('px-6 py-2.5', 'px-4 py-1.5');

export const card = 'rounded-xl bg-white card-shadow';

export const tableHead = 'bg-gray-50 text-xs font-medium uppercase tracking-wider text-gray-500';
export const tableRow = 'text-gray-700 transition-colors hover:bg-gray-50/50';

/** Text input / select / textarea. Pass true when the field has an error. */
export const inputClass = (invalid = false) => [
  'w-full rounded-lg border bg-white px-3 py-2.5 text-sm text-gray-700 focus:outline-none focus:ring-2',
  invalid
    ? 'border-rose-400 focus:border-rose-400 focus:ring-rose-200'
    : 'border-gray-200 focus:border-sky-400 focus:ring-sky-300',
];

export const badgeSky = 'inline-flex items-center rounded-full bg-sky-100 px-2 py-0.5 text-[11px] font-medium text-sky-600';
export const badgeAmber = 'inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-medium text-amber-600';
