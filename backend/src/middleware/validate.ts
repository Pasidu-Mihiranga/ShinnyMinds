import type { NextFunction, Request, RequestHandler, Response } from 'express';
import { ZodError, type ZodTypeAny, type z } from 'zod';
import { HttpError } from '../lib/http-error.js';

/**
 * Validates and replaces the request payload with the parsed result, so controllers
 * receive data that is already the right shape and type. Rejections become a 400
 * listing every offending field rather than one error at a time.
 */
export function validateBody<T extends ZodTypeAny>(schema: T): RequestHandler {
  return (req: Request, _res: Response, next: NextFunction) => {
    try {
      req.body = schema.parse(req.body) as z.infer<T>;

      next();
    } catch (error) {
      next(toHttpError(error));
    }
  };
}

export function validateQuery<T extends ZodTypeAny>(schema: T): RequestHandler {
  return (req: Request, _res: Response, next: NextFunction) => {
    try {
      req.query = schema.parse(req.query) as z.infer<T> & Request['query'];

      next();
    } catch (error) {
      next(toHttpError(error));
    }
  };
}

function toHttpError(error: unknown): unknown {
  if (error instanceof ZodError) {
    return HttpError.badRequest(
      'Some of the values sent were not valid.',
      error.issues.map((issue) => ({
        field: issue.path.join('.') || '(root)',
        message: issue.message,
      })),
    );
  }

  return error;
}
