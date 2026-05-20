/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './*.tsx',
    './{api,components,pages}/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        // Tesla-inspired palette from DESIGN.md
        brand: {
          DEFAULT: '#3E6AE1',   // Electric Blue — primary CTA / links
          hover: '#2F54C6',     // darker on hover
        },
        surface: {
          DEFAULT: '#FFFFFF',   // pure white background
          subtle: '#F7F8FA',    // very light gray for alternating rows
        },
        text: {
          primary: '#171A20',   // near-black headings / body
          secondary: '#5C5E62', // medium gray captions / labels
          tertiary: '#949699',  // muted placeholders
        },
        border: {
          DEFAULT: '#E8E9EA',   // subtle dividers
        },
        error: {
          DEFAULT: '#CF4939',   // Tesla red for errors / destructive
          bg: '#FFF1F0',        // light error background
        },
        success: {
          DEFAULT: '#0A7470',   // teal/green for success states
          bg: '#E6F8F7',        // light success background
        },
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', '-apple-system', 'sans-serif'],
      },
      fontWeight: {
        // Tesla design uses only 400 and 500 — no bold
        normal: '400',
        medium: '500',
      },
      borderRadius: {
        DEFAULT: '4px',   // interactive elements
        lg: '4px',        // override default lg to keep consistent
        xl: '4px',
        '2xl': '4px',
        full: '4px',      // even pills get 4px — Tesla aesthetic
      },
    },
  },
  plugins: [],
};
