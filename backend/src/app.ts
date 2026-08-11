import cors from 'cors';
import express from 'express';
import helmet from 'helmet';
import morgan from 'morgan';
import { env } from './config/env.js';
import { errorHandler, notFoundHandler } from './middleware/error-handler.js';
import { authRouter } from './routes/auth.routes.js';
import { gameRouter } from './routes/game.routes.js';
import { dashboardRouter } from './routes/dashboard.routes.js';
import { groqService } from './services/groq.service.js';

export function createApp() {
  const app = express();

  app.use(helmet());

  app.use(
    cors({
      origin(origin, callback) {
        // Requests without an Origin header are non-browser clients - notably the
        // Unity player, which is not subject to the same-origin policy at all.
        if (!origin || env.corsOrigins.includes(origin)) {
          return callback(null, true);
        }

        return callback(new Error(`Origin ${origin} is not allowed by CORS_ORIGINS.`));
      },
      credentials: true,
    }),
  );

  app.use(express.json({ limit: '256kb' }));

  if (!env.isProduction) {
    app.use(morgan('dev'));
  }

  app.get('/health', (_req, res) => {
    res.json({
      status: 'ok',
      environment: env.NODE_ENV,
      assistantConfigured: groqService.isConfigured,
      time: new Date().toISOString(),
    });
  });

  app.use('/api/auth', authRouter);
  app.use('/api/game', gameRouter);
  app.use('/api/dashboard', dashboardRouter);

  app.use(notFoundHandler);
  app.use(errorHandler);

  return app;
}
