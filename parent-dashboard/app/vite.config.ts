import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['images/logo.svg'],
      manifest: {
        name: 'Shinyminds Parent',
        short_name: 'Shinyminds',
        description: 'Parent dashboard for tracking your child\'s social-emotional learning progress.',
        theme_color: '#7c3aed',
        background_color: '#f2f1f7',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: '/images/logo.svg', sizes: '512x512', type: 'image/svg+xml', purpose: 'any' },
          { src: '/images/logo.svg', sizes: '512x512', type: 'image/svg+xml', purpose: 'maskable' },
        ],
      },
    }),
  ],
})
