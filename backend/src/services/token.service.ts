import crypto from 'node:crypto';
import jwt from 'jsonwebtoken';
import { env } from '../config/env.js';
import { HttpError } from '../lib/http-error.js';
import { accountRepository } from '../repositories/account.repository.js';

export type AccountRole = 'parent' | 'child';

export interface AccessTokenPayload {
  sub: string;
  role: AccountRole;
}

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  expiresIn: string;
}

/**
 * Access tokens are short-lived JWTs sent on every request. Refresh tokens are opaque
 * random strings; only their SHA-256 hash is stored, so a database leak does not hand
 * an attacker usable sessions.
 */
export const tokenService = {
  async issue(subjectId: string, role: AccountRole): Promise<TokenPair> {
    const accessToken = jwt.sign({ sub: subjectId, role } satisfies AccessTokenPayload, env.JWT_ACCESS_SECRET, {
      expiresIn: env.ACCESS_TOKEN_TTL,
    } as jwt.SignOptions);

    const refreshToken = crypto.randomBytes(48).toString('base64url');

    const expiresAt = new Date();
    expiresAt.setDate(expiresAt.getDate() + env.REFRESH_TOKEN_TTL_DAYS);

    await accountRepository.createRefreshToken({
      tokenHash: hash(refreshToken),
      expiresAt,
      parentId: role === 'parent' ? subjectId : null,
      childId: role === 'child' ? subjectId : null,
    });

    return { accessToken, refreshToken, expiresIn: env.ACCESS_TOKEN_TTL };
  },

  verifyAccessToken(token: string): AccessTokenPayload {
    try {
      const decoded = jwt.verify(token, env.JWT_ACCESS_SECRET);

      if (typeof decoded === 'string' || typeof decoded.sub !== 'string') {
        throw HttpError.unauthorized('Malformed access token.');
      }

      const role = (decoded as jwt.JwtPayload).role;

      if (role !== 'parent' && role !== 'child') {
        throw HttpError.unauthorized('Malformed access token.');
      }

      return { sub: decoded.sub, role };
    } catch (error) {
      if (error instanceof HttpError) {
        throw error;
      }

      throw HttpError.unauthorized('Your session has expired. Please sign in again.');
    }
  },

  /**
   * Exchanges a refresh token for a new pair. The presented token is revoked in the
   * same step (rotation), so a stolen refresh token is usable at most once.
   */
  async rotate(refreshToken: string): Promise<TokenPair & { role: AccountRole; subjectId: string }> {
    const stored = await accountRepository.findRefreshToken(hash(refreshToken));

    if (!stored || stored.revokedAt || stored.expiresAt < new Date()) {
      throw HttpError.unauthorized('Your session has expired. Please sign in again.');
    }

    // The revoke is the point at which this token is claimed, and only one caller can
    // win it: the update matches on revokedAt IS NULL, so a second request racing with
    // the first updates no rows. Ignoring that count would have let one refresh token
    // be redeemed twice, defeating the rotation.
    const claimed = await accountRepository.revokeRefreshToken(stored.tokenHash);

    if (claimed === 0) {
      throw HttpError.unauthorized('Your session has expired. Please sign in again.');
    }

    const role: AccountRole = stored.parentId ? 'parent' : 'child';
    const subjectId = stored.parentId ?? stored.childId;

    if (!subjectId) {
      throw HttpError.unauthorized('Your session has expired. Please sign in again.');
    }

    const pair = await this.issue(subjectId, role);

    return { ...pair, role, subjectId };
  },

  async revoke(refreshToken: string): Promise<void> {
    await accountRepository.revokeRefreshToken(hash(refreshToken));
  },
};

function hash(token: string): string {
  return crypto.createHash('sha256').update(token).digest('hex');
}
