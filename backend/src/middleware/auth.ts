import type { NextFunction, Request, Response } from 'express';
import { HttpError } from '../lib/http-error.js';
import { tokenService, type AccountRole } from '../services/token.service.js';

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Express {
    interface Request {
      account?: { id: string; role: AccountRole };
    }
  }
}

/**
 * Verifies the bearer token and pins the request to one account.
 *
 * `requiredRole` matters: a child's game token must not be able to read the parent
 * dashboard, and a parent token must not be able to write gameplay records.
 */
export function authenticate(requiredRole?: AccountRole) {
  return (req: Request, _res: Response, next: NextFunction) => {
    const header = req.headers.authorization;

    if (!header?.startsWith('Bearer ')) {
      return next(HttpError.unauthorized('Missing bearer token.'));
    }

    try {
      const payload = tokenService.verifyAccessToken(header.slice('Bearer '.length).trim());

      if (requiredRole && payload.role !== requiredRole) {
        return next(HttpError.forbidden(`This endpoint requires a ${requiredRole} account.`));
      }

      req.account = { id: payload.sub, role: payload.role };

      return next();
    } catch (error) {
      return next(error);
    }
  };
}

/** Reads the authenticated account, or throws. Keeps controllers free of null checks. */
export function currentAccount(req: Request): { id: string; role: AccountRole } {
  if (!req.account) {
    throw HttpError.unauthorized();
  }

  return req.account;
}
