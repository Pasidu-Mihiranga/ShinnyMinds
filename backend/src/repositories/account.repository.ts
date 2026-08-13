import type { Child, Parent, RefreshToken } from '@prisma/client';
import { prisma } from '../lib/prisma.js';

/**
 * Data access for parent and child accounts and their refresh tokens.
 * Repositories are the only place Prisma is called; services stay free of query syntax.
 */
export const accountRepository = {
  findParentByEmail(email: string): Promise<Parent | null> {
    return prisma.parent.findUnique({ where: { email } });
  },

  findParentById(id: string): Promise<Parent | null> {
    return prisma.parent.findUnique({ where: { id } });
  },

  findParentByLinkCode(linkCode: string): Promise<Parent | null> {
    return prisma.parent.findUnique({ where: { linkCode } });
  },

  createParent(data: {
    email: string;
    passwordHash: string;
    displayName: string;
    linkCode: string;
  }): Promise<Parent> {
    return prisma.parent.create({ data });
  },

  findChildByUsername(username: string): Promise<Child | null> {
    return prisma.child.findUnique({ where: { username } });
  },

  findChildById(id: string): Promise<Child | null> {
    return prisma.child.findUnique({ where: { id } });
  },

  listChildrenForParent(parentId: string): Promise<Child[]> {
    return prisma.child.findMany({
      where: { parentId },
      orderBy: { createdAt: 'asc' },
    });
  },

  createChild(data: {
    username: string;
    passwordHash: string;
    displayName: string;
    age?: number | null;
    parentId?: string | null;
  }): Promise<Child> {
    return prisma.child.create({ data });
  },

  linkChildToParent(childId: string, parentId: string): Promise<Child> {
    return prisma.child.update({
      where: { id: childId },
      data: { parentId },
    });
  },

  // --- refresh tokens -------------------------------------------------------

  createRefreshToken(data: {
    tokenHash: string;
    parentId?: string | null;
    childId?: string | null;
    expiresAt: Date;
  }): Promise<RefreshToken> {
    return prisma.refreshToken.create({ data });
  },

  findRefreshToken(tokenHash: string): Promise<RefreshToken | null> {
    return prisma.refreshToken.findUnique({ where: { tokenHash } });
  },

  revokeRefreshToken(tokenHash: string): Promise<number> {
    return prisma.refreshToken
      .updateMany({
        where: { tokenHash, revokedAt: null },
        data: { revokedAt: new Date() },
      })
      .then((result) => result.count);
  },

  revokeAllForSubject(subject: { parentId?: string; childId?: string }): Promise<number> {
    return prisma.refreshToken
      .updateMany({
        where: { ...subject, revokedAt: null },
        data: { revokedAt: new Date() },
      })
      .then((result) => result.count);
  },
};
