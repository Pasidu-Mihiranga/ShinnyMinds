import type { NextFunction, Request, Response } from 'express';
import { Prisma } from '@prisma/client';
import { env } from '../config/env.js';
import { HttpError } from '../lib/http-error.js';

export function notFoundHandler(req: Request, res: Response) {
  res.status(404).json({
    error: { code: 'NOT_FOUND', message: `No route for ${req.method} ${req.path}.` },
  });
}

/**
 * Single place where an error becomes a response. Known failures keep their message;
 * anything else is logged in full and reported as a generic 500, so stack traces and
 * SQL never reach a client.
 */
export function errorHandler(
  error: unknown,
  _req: Request,
  res: Response,
  _next: NextFunction,
) {
  if (error instanceof HttpError) {
    return res.status(error.status).json({
      error: {
        code: error.code,
        message: error.message,
        ...(error.details ? { details: error.details } : {}),
      },
    });
  }

  if (error instanceof Prisma.PrismaClientKnownRequestError) {
    if (error.code === 'P2002') {
      return res.status(409).json({
        error: { code: 'CONFLICT', message: 'That value is already taken.' },
      });
    }

    if (error.code === 'P2025') {
      return res.status(404).json({
        error: { code: 'NOT_FOUND', message: 'Resource not found.' },
      });
    }
  }

  console.error('[error]', error);

  return res.status(500).json({
    error: {
      code: 'INTERNAL_ERROR',
      message: 'Something went wrong on our side. Please try again.',
      ...(env.isProduction ? {} : { debug: error instanceof Error ? error.message : String(error) }),
    },
  });
}
