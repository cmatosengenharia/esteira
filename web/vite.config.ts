import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Em produção o build vai para src/Esteira.Api/wwwroot e a API serve tudo na mesma origem.
// Em desenvolvimento o Vite faz proxy para a API em :5080.
export default defineConfig({
  plugins: [react()],
  build: { outDir: "../src/Esteira.Api/wwwroot", emptyOutDir: true },
  server: {
    port: 5173,
    proxy: {
      "/jobs": "http://localhost:5080",
      "/dev": "http://localhost:5080",
      "/metrics": "http://localhost:5080",
      "/health": "http://localhost:5080",
    },
  },
});
