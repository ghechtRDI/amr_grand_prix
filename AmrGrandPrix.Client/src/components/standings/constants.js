export const STICKY_HEAD =
  'sticky top-0 z-10 h-9 whitespace-nowrap bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black';

/** Gender toggle order — Female first, and the default. */
export const GENDERS = ['female', 'male'];

export const genderLabel = (g) => (g === 'male' ? 'Male' : 'Female');
