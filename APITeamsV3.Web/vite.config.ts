import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    allowedHosts: [
      'teams.zegel.edu.pe',
      'teams.idat.edu.pe',
      'teams.corrientealterna.edu.pe',
      'teams.its.edu.pe',
      'teams.centrodelaimagen.pe',
    ],
  },
})
