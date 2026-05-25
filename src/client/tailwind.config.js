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
        brand: {
          DEFAULT: '#7C3FF2',
          hover: '#6D32DD',
          navy: '#071333',
          teal: '#14C8B6',
          blue: '#2F6FF2',
        },
        movies: '#14C8B6',
        'movies-light': 'rgba(20,200,182,0.12)',
        'movies-border': 'rgba(20,200,182,0.28)',
        music: '#7C3FF2',
        'music-light': 'rgba(124,63,242,0.12)',
        'music-border': 'rgba(124,63,242,0.28)',
        games: '#2F6FF2',
        'games-light': 'rgba(47,111,242,0.12)',
        'games-border': 'rgba(47,111,242,0.28)',
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
          DEFAULT: '#D94C4C',   // red for errors / destructive
          bg: '#FFF1F0',        // light error background
        },
        success: {
          DEFAULT: '#16C7A9',
          bg: '#E8FBF7',
        },
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', '-apple-system', 'sans-serif'],
      },
      fontWeight: {
        normal: '400',
        medium: '500',
        semibold: '600',
        bold: '700',
      },
      borderRadius: {
        DEFAULT: '12px',
        sm: '8px',
        lg: '18px',
        xl: '24px',
        '2xl': '28px',
        pill: '9999px',
        full: '9999px',    // maximum pill shape
      },
      boxShadow: {
        card: 'var(--shadow-card)',
      },
    },
  },
  plugins: [],
};
