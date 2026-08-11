import { Router } from 'express';
import { asyncHandler } from '../lib/async-handler.js';
import { authenticate, currentAccount } from '../middleware/auth.js';
import { validateBody } from '../middleware/validate.js';
import {
  childLoginSchema,
  childRegisterSchema,
  linkParentSchema,
  parentLoginSchema,
  parentRegisterSchema,
  refreshSchema,
} from '../dto/index.js';
import { authService } from '../services/auth.service.js';
import { tokenService } from '../services/token.service.js';

export const authRouter = Router();

// --- parent (dashboard) -----------------------------------------------------

authRouter.post(
  '/parent/register',
  validateBody(parentRegisterSchema),
  asyncHandler(async (req, res) => {
    const result = await authService.registerParent(req.body);

    res.status(201).json(result);
  }),
);

authRouter.post(
  '/parent/login',
  validateBody(parentLoginSchema),
  asyncHandler(async (req, res) => {
    res.json(await authService.loginParent(req.body));
  }),
);

// --- child (game) -----------------------------------------------------------

authRouter.post(
  '/player/register',
  validateBody(childRegisterSchema),
  asyncHandler(async (req, res) => {
    const result = await authService.registerChild(req.body);

    res.status(201).json(result);
  }),
);

authRouter.post(
  '/player/login',
  validateBody(childLoginSchema),
  asyncHandler(async (req, res) => {
    res.json(await authService.loginChild(req.body));
  }),
);

authRouter.post(
  '/player/link-parent',
  authenticate('child'),
  validateBody(linkParentSchema),
  asyncHandler(async (req, res) => {
    const account = currentAccount(req);

    res.json({ child: await authService.linkToParent(account.id, req.body.parentLinkCode) });
  }),
);

// --- shared -----------------------------------------------------------------

authRouter.post(
  '/refresh',
  validateBody(refreshSchema),
  asyncHandler(async (req, res) => {
    const { role, subjectId, ...tokens } = await tokenService.rotate(req.body.refreshToken);

    res.json({ tokens, role, accountId: subjectId });
  }),
);

authRouter.post(
  '/logout',
  validateBody(refreshSchema),
  asyncHandler(async (req, res) => {
    await tokenService.revoke(req.body.refreshToken);

    res.status(204).send();
  }),
);

authRouter.get(
  '/me',
  authenticate(),
  asyncHandler(async (req, res) => {
    const account = currentAccount(req);

    if (account.role === 'parent') {
      res.json({ role: 'parent', parent: await authService.parentById(account.id) });

      return;
    }

    res.json({ role: 'child', child: await authService.childById(account.id) });
  }),
);
