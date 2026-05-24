/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './*.tsx',
    './{api,components,pages}/**/*.{ts,tsx}',
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // Coinbase-inspired palette
        brand: {
          DEFAULT: '#0052ff',   // Coinbase Blue — primary CTA / links (neutral fallback)
          hover: '#578bfa',     // lighter blue on hover
        },
        // Per-category accent colors (flat keys for Tailwind detection)
        movies: '#f59e0b',
        'movies-light': 'rgba(245,158,11,0.1)',
        'movies-border': 'rgba(245,158,11,0.2)',
        music: '#8b5cf6',
        'music-light': 'rgba(139,92,246,0.1)',
        'music-border': 'rgba(139,92,246,0.2)',
        games: '#14b8a6',
        'games-light': 'rgba(20,184,166,0.1)',
        'games-border': 'rgba(20,184,166,0.2)',
        surface: 'var(--color-surface)',       // page background (adapts)
        card: 'var(--color-card)',             // cards/panels (adapts)
        text: {
          primary: 'var(--color-text-primary)',     // headings / body (adapts)
          secondary: 'var(--color-text-secondary)',  // captions / labels (adapts)
          tertiary: 'var(--color-text-tertiary)',    // placeholders (adapts)
        },
        border: {
          DEFAULT: 'var(--card-border)',   // subtle dividers (adapts)
        },
        input: {
          bg: 'var(--color-input-bg)',       // input/select background (adapts)
          border: 'var(--color-input-border)', // input border (adapts)
          placeholder: 'var(--color-placeholder)', // placeholder text (adapts)
        },
        pill: {
          bg: 'var(--color-pill-bg)',       // pill/badge background (adapts)
          border: 'var(--color-pill-border)', // pill border (adapts)
        },
        divider: 'var(--color-divider)',    // list dividers (adapts)
        imgPlaceholder: 'var(--color-image-placeholder)', // image placeholder bg (adapts)
        error: {
          DEFAULT: '#CF4939',   // red for errors / destructive
          bg: '#FFF1F0',        // light error background
        },
        success: {
          DEFAULT: '#0A7470',   // teal/green for success states
          bg: '#E6F8F7',        // light success background
        },
      },
      fontFamily: {
        sans: ['DM Sans', 'ui-sans-serif', 'system-ui', '-apple-system', 'sans-serif'],
      },
      fontWeight: {
        normal: '400',
        medium: '500',
        semibold: '600',
        bold: '700',
      },
      borderRadius: {
        DEFAULT: '8px',    // Coinbase standard card radius
        sm: '4px',         // small elements
        lg: '12px',        // cards, menus
        xl: '16px',        // larger containers
        '2xl': '24px',     // feature sections
        pill: '56px',      // Coinbase CTA buttons
        full: '9999px',    // maximum pill shape
      },
    },
  },
  plugins: [],
};
