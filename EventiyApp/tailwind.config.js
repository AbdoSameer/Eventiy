/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        // Meetup-inspired Coral Red palette
        primary: {
          DEFAULT: '#F6544C',
          dark: '#E53E3E',
          light: '#FF7B73',
        },
        background: {
          DEFAULT: '#FFFFFF',
          alt: '#F7F7F7',
        },
        'text-primary': '#1F2937',
        'text-secondary': '#6B7280',
        border: {
          DEFAULT: '#E5E7EB',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        card: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
      },
      animation: {
        'slide-in': 'slideIn 0.3s ease-out',
      },
      keyframes: {
        slideIn: {
          '0%': { transform: 'translateX(100%)' },
          '100%': { transform: 'translateX(0)' },
        },
      },
    },
  },
  plugins: [],
}
