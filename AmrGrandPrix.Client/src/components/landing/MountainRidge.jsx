/**
 * MountainRidge
 * Decorative layered ridgeline for the landing-page hero: two tinted back ranges, a dashed trail
 * climbing to a flag on the high peak, and a front range filled with the page background so the
 * next section appears to rest on the mountains. Colors come from theme tokens, so it re-tints
 * for light/dark mode with no assets.
 */

import { cn } from '@/lib/utils';

export default function MountainRidge({ className }) {
  return (
    <svg
      viewBox="0 0 1440 320"
      preserveAspectRatio="xMidYMax slice"
      aria-hidden="true"
      focusable="false"
      className={cn('block w-full', className)}
    >
      {/* Sun / midnight sun */}
      <circle cx="930" cy="128" r="30" className="fill-secondary/60" />

      {/* Far range */}
      <path
        d="M0 230 L120 170 L210 205 L340 110 L450 180 L560 130 L700 200 L820 120 L940 175 L1060 90 L1190 170 L1300 135 L1440 190 L1440 320 L0 320 Z"
        className="fill-primary/15"
      />

      {/* Middle range with snowcap on the high peak */}
      <path
        d="M0 270 L150 215 L260 245 L420 150 L540 225 L660 185 L780 240 L900 160 L1000 200 L1120 120 L1250 215 L1360 185 L1440 220 L1440 320 L0 320 Z"
        className="fill-primary/30"
      />
      <path d="M1120 120 L1093 145 L1108 141 L1120 152 L1134 140 L1149 146 Z" className="fill-white/85" />

      {/* Trail to the summit flag */}
      <path
        d="M760 300 C 840 280, 870 250, 940 238 S 1040 200, 1070 172 S 1105 140, 1120 124"
        fill="none"
        strokeWidth="3"
        strokeLinecap="round"
        strokeDasharray="2 10"
        className="stroke-foreground/45"
      />
      <line x1="1120" y1="124" x2="1120" y2="88" strokeWidth="3" strokeLinecap="round" className="stroke-foreground/70" />
      <path d="M1121 89 L1148 97 L1121 106 Z" className="fill-secondary" />

      {/* Near range: page background, so the following section sits on the ridge */}
      <path
        d="M0 300 L180 265 L300 285 L470 230 L620 280 L760 255 L900 290 L1040 250 L1180 285 L1320 260 L1440 280 L1440 320 L0 320 Z"
        className="fill-background"
      />
    </svg>
  );
}
