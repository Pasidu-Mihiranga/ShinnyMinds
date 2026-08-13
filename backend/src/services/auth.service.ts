import crypto from 'node:crypto';
import bcrypt from 'bcryptjs';
import type { Child, Parent } from '@prisma/client';
import { HttpError } from '../lib/http-error.js';
import { accountRepository } from '../repositories/account.repository.js';
import { tokenService, type TokenPair } from './token.service.js';

const BCRYPT_ROUNDS = 12;

// Ambiguous characters (0/O, 1/I) are excluded: children type this code by hand.
const LINK_CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const LINK_CODE_LENGTH = 6;

export interface ParentView {
  id: string;
  email: string;
  displayName: string;
  linkCode: string;
}

export interface ChildView {
  id: string;
  username: string;
  displayName: string;
  age: number | null;
  avatarUrl: string | null;
  isLinkedToParent: boolean;
}

export const authService = {
  async registerParent(input: {
    email: string;
    password: string;
    displayName: string;
  }): Promise<{ parent: ParentView; tokens: TokenPair }> {
    const email = input.email.trim().toLowerCase();

    if (await accountRepository.findParentByEmail(email)) {
      throw HttpError.conflict('An account with that email already exists.');
    }

    const parent = await accountRepository.createParent({
      email,
      displayName: input.displayName.trim(),
      passwordHash: await bcrypt.hash(input.password, BCRYPT_ROUNDS),
      linkCode: await generateUniqueLinkCode(),
    });

    return {
      parent: toParentView(parent),
      tokens: await tokenService.issue(parent.id, 'parent'),
    };
  },

  async loginParent(input: {
    email: string;
    password: string;
  }): Promise<{ parent: ParentView; tokens: TokenPair }> {
    const parent = await accountRepository.findParentByEmail(input.email.trim().toLowerCase());

    // Same message and comparable timing for "no such account" and "wrong password",
    // so the endpoint cannot be used to discover which emails are registered.
    if (!parent || !(await bcrypt.compare(input.password, parent.passwordHash))) {
      throw HttpError.unauthorized('Incorrect email or password.');
    }

    return {
      parent: toParentView(parent),
      tokens: await tokenService.issue(parent.id, 'parent'),
    };
  },

  async registerChild(input: {
    username: string;
    password: string;
    displayName: string;
    age?: number;
    parentLinkCode?: string;
  }): Promise<{ child: ChildView; tokens: TokenPair }> {
    const username = input.username.trim().toLowerCase();

    if (await accountRepository.findChildByUsername(username)) {
      throw HttpError.conflict('That username is already taken. Please pick another one.');
    }

    let parentId: string | null = null;

    if (input.parentLinkCode) {
      const parent = await accountRepository.findParentByLinkCode(
        input.parentLinkCode.trim().toUpperCase(),
      );

      if (!parent) {
        throw HttpError.badRequest('That parent code was not recognised. Check it and try again.');
      }

      parentId = parent.id;
    }

    const child = await accountRepository.createChild({
      username,
      displayName: input.displayName.trim(),
      passwordHash: await bcrypt.hash(input.password, BCRYPT_ROUNDS),
      age: input.age ?? null,
      parentId,
    });

    return {
      child: toChildView(child),
      tokens: await tokenService.issue(child.id, 'child'),
    };
  },

  async loginChild(input: {
    username: string;
    password: string;
  }): Promise<{ child: ChildView; tokens: TokenPair }> {
    const child = await accountRepository.findChildByUsername(input.username.trim().toLowerCase());

    if (!child || !(await bcrypt.compare(input.password, child.passwordHash))) {
      throw HttpError.unauthorized('Incorrect username or password.');
    }

    return {
      child: toChildView(child),
      tokens: await tokenService.issue(child.id, 'child'),
    };
  },

  /** Lets a child who registered without a code attach to a parent later. */
  async linkToParent(childId: string, linkCode: string): Promise<ChildView> {
    const parent = await accountRepository.findParentByLinkCode(linkCode.trim().toUpperCase());

    if (!parent) {
      throw HttpError.badRequest('That parent code was not recognised. Check it and try again.');
    }

    return toChildView(await accountRepository.linkChildToParent(childId, parent.id));
  },

  async parentById(id: string): Promise<ParentView> {
    const parent = await accountRepository.findParentById(id);

    if (!parent) {
      throw HttpError.unauthorized('Account no longer exists.');
    }

    return toParentView(parent);
  },

  async childById(id: string): Promise<ChildView> {
    const child = await accountRepository.findChildById(id);

    if (!child) {
      throw HttpError.unauthorized('Account no longer exists.');
    }

    return toChildView(child);
  },
};

export function toParentView(parent: Parent): ParentView {
  return {
    id: parent.id,
    email: parent.email,
    displayName: parent.displayName,
    linkCode: parent.linkCode,
  };
}

export function toChildView(child: Child): ChildView {
  return {
    id: child.id,
    username: child.username,
    displayName: child.displayName,
    age: child.age,
    avatarUrl: child.avatarUrl,
    isLinkedToParent: Boolean(child.parentId),
  };
}

async function generateUniqueLinkCode(): Promise<string> {
  for (let attempt = 0; attempt < 10; attempt += 1) {
    const code = Array.from(
      crypto.randomBytes(LINK_CODE_LENGTH),
      (byte) => LINK_CODE_ALPHABET[byte % LINK_CODE_ALPHABET.length],
    ).join('');

    if (!(await accountRepository.findParentByLinkCode(code))) {
      return code;
    }
  }

  throw new Error('Could not generate a unique parent link code.');
}
