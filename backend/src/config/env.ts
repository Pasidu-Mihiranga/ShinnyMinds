import 'dotenv/config';
import { z } from 'zod';

/**
 * Every environment variable the server reads, validated once at boot.
 *
 * Validating here means a missing JWT secret fails immediately with a readable
 * message, rather than surfacing later as tokens that silently verify against
 * `undefined`.
 */
const schema = z.object({
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
  PORT: z.coerce.number().int().positive().default(4000),

  DATABASE_URL: z.string().min(1, 'DATABASE_URL is required (see .env.example)'),

  CORS_ORIGINS: z.string().default('http://localhost:5173'),

  JWT_ACCESS_SECRET: z
    .string()
    .min(32, 'JWT_ACCESS_SECRET must be at least 32 characters. Generate one with: openssl rand -base64 48'),
  JWT_REFRESH_SECRET: z
    .string()
    .min(32, 'JWT_REFRESH_SECRET must be at least 32 characters. Generate one with: openssl rand -base64 48'),
  ACCESS_TOKEN_TTL: z.string().default('15m'),
  REFRESH_TOKEN_TTL_DAYS: z.coerce.number().int().positive().default(30),

  // Optional: the assistant degrades to a clear "not configured" message without it,
  // so the rest of the dashboard still works on a fresh checkout.
  GROQ_API_KEY: z.string().optional(),
  GROQ_MODEL: z.string().default('llama-3.3-70b-versatile'),
});

const parsed = schema.safeParse(process.env);

if (!parsed.success) {
  const details = parsed.error.issues
    .map((issue) => `  - ${issue.path.join('.')}: ${issue.message}`)
    .join('\n');

  throw new Error(`Invalid environment configuration:\n${details}\n\nCopy backend/.env.example to backend/.env and fill it in.`);
}

const raw = parsed.data;

export const env = {
  ...raw,
  isProduction: raw.NODE_ENV === 'production',
  corsOrigins: raw.CORS_ORIGINS.split(',')
    .map((origin) => origin.trim())
    .filter(Boolean),
  hasGroq: Boolean(raw.GROQ_API_KEY && raw.GROQ_API_KEY.length > 0),
};

export type Env = typeof env;
