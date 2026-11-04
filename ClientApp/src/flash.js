import { reactive } from 'vue';

/** One banner message shown at the top of the page, for results like "Team saved". */
export const flash = reactive({ text: '', kind: 'success' });

let timer;

export function showFlash(text, kind = 'success') {
  flash.text = text;
  flash.kind = kind;
  clearTimeout(timer);
  timer = setTimeout(clearFlash, kind === 'error' ? 8000 : 4500);
}

export function clearFlash() {
  clearTimeout(timer);
  flash.text = '';
}
