module.exports = {
  darkMode: "class",
  content: [
    "./pages/**/*.{js,ts,jsx,tsx}",
    "./components/**/*.{js,ts,jsx,tsx}",
    "./app/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    container: {
      center: true,
      padding: "2rem", // 👈 questo imposta px-8 (2rem) come default
      screens: {
        xl: "1400px", // 👈 imposta max-width per xl (1280px) a 1400px
      },
    },
    extend: {
      fontFamily: {
        sans: ["Satoshi", "sans-serif"],
      },
      colors: {
        // 🎨 DataPolar Palette
        articWhite: "#F8F9FA",
        softWhite: "#F8F9FA",
        glacierBlue: "#A7C6ED",
        polarNight: "#1C2D44",
        frozenGrey: "#DDE3EA",
        dataRed: "#E94F4F",
        coldIndigo: "#5C4DE1",
      },
      backgroundImage: {
        "hero-grid":
          "radial-gradient(circle at center, rgba(92, 77, 225, 0.15) 0%, transparent 60%), \
          linear-gradient(#ffffff0a 1px, transparent 1px), \
          linear-gradient(to right, #ffffff0a 1px, transparent 1px)",
        "hero-grid-light":
          "radial-gradient(circle at center, rgba(92, 77, 225, 0.1) 0%, transparent 60%), \
          linear-gradient(#0000000a 1px, transparent 1px), \
          linear-gradient(to right, #0000000a 1px, transparent 1px)",
        "gradient-radial": "radial-gradient(var(--tw-gradient-stops))",
        "products-hero": `
          radial-gradient(at 30% 20%, rgba(92, 77, 225, 0.10) 0%, transparent 60%),
          radial-gradient(at 70% 80%, rgba(92, 77, 225, 0.08) 0%, transparent 60%),
          radial-gradient(at 50% 90%, rgba(92, 77, 225, 0.06) 0%, transparent 60%)`,
        "products-hero-light": `
          radial-gradient(at 25% 30%, rgba(92, 77, 225, 0.06) 0%, transparent 65%),
          radial-gradient(at 80% 70%, rgba(92, 77, 225, 0.05) 0%, transparent 65%),
          radial-gradient(at 50% 90%, rgba(92, 77, 225, 0.04) 0%, transparent 60%)`,
      },
      backgroundSize: {
        "hero-grid": "40px 40px",
      },
    },
  },
  plugins: [],
};
