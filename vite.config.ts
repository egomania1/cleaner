import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"
import tailwindcss from "@tailwindcss/vite"
import electron from "vite-plugin-electron/simple"
import renderer from "vite-plugin-electron-renderer"
import path from "node:path"

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    electron({
      main: {
        entry: "electron/main.ts",
        vite: {
          build: {
            outDir: "dist-electron",
          },
        },
      },
      preload: {
        input: path.join(__dirname, "electron/preload.ts"),
        vite: {
          build: {
            outDir: "dist-electron",
          },
        },
      },
    }),
    renderer(),
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
})
