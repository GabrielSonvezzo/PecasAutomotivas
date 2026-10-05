/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./Views/**/*.cshtml', './wwwroot/js/**/*.js', './Helpers/**/*.cs', './wwwroot/offline.html'],
  theme: {
    extend: {
      colors: {
        carbon: { 950: '#0A0A0A', 900: '#141414', 800: '#1F1F1F', 700: '#2A2A2A', 600: '#3A3A3A' },
        laranja: { DEFAULT: '#FF6B00', hover: '#E65C00', claro: '#FF8A33' },
        tecnico: '#A3A3A3'
      },
      fontFamily: {
        sans: ['Barlow', 'system-ui', 'Segoe UI', 'Roboto', 'sans-serif'],
        display: ['"Barlow Condensed"', 'Barlow', 'system-ui', 'sans-serif']
      },
      maxWidth: { site: '80rem' }
    }
  },
  plugins: []
};
