import { createApp } from './app.js';
import { env } from './config/env.js';
import { prisma } from './lib/prisma.js';

const app = createApp();

const server = app.listen(env.PORT, () => {
  console.log(`ShinyMinds API listening on http://localhost:${env.PORT} (${env.NODE_ENV})`);

  if (!env.hasGroq) {
    console.warn('GROQ_API_KEY is not set - the parent assistant will return 503 until it is.');
  }
});

// Close the HTTP server and the database pool on shutdown so `tsx watch` restarts
// cleanly and containers stop without dropping in-flight requests.
for (const signal of ['SIGINT', 'SIGTERM'] as const) {
  process.on(signal, () => {
    console.log(`\nReceived ${signal}, shutting down.`);

    server.close(() => {
      void prisma.$disconnect().then(() => process.exit(0));
    });
  });
}
