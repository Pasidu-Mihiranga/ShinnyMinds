import { PrismaClient } from '@prisma/client';
import { env } from '../config/env.js';

/**
 * Single shared Prisma client. Reused across a `tsx watch` reload so repeated
 * restarts do not exhaust the PostgreSQL connection pool.
 */
const globalForPrisma = globalThis as unknown as { prisma?: PrismaClient };

export const prisma =
  globalForPrisma.prisma ??
  new PrismaClient({
    log: env.isProduction ? ['warn', 'error'] : ['warn', 'error'],
  });

if (!env.isProduction) {
  globalForPrisma.prisma = prisma;
}
