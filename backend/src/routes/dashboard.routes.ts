import { Router } from 'express';
import { asyncHandler } from '../lib/async-handler.js';
import { authenticate, currentAccount } from '../middleware/auth.js';
import { validateBody, validateQuery } from '../middleware/validate.js';
import { activityQuerySchema, chatSendSchema, skillsProgressQuerySchema } from '../dto/index.js';
import { dashboardService } from '../services/dashboard.service.js';
import { chatService } from '../services/chat.service.js';

/**
 * Called by the parent dashboard. Every route requires a parent token, and each
 * service method re-checks that the requested child belongs to that parent.
 */
export const dashboardRouter = Router();

dashboardRouter.use(authenticate('parent'));

dashboardRouter.get(
  '/children',
  asyncHandler(async (req, res) => {
    res.json({ children: await dashboardService.children(currentAccount(req).id) });
  }),
);

dashboardRouter.get(
  '/children/:childId/overview',
  asyncHandler(async (req, res) => {
    res.json(await dashboardService.overview(currentAccount(req).id, req.params.childId as string));
  }),
);

dashboardRouter.get(
  '/children/:childId/skills/progress',
  validateQuery(skillsProgressQuerySchema),
  asyncHandler(async (req, res) => {
    res.json(
      await dashboardService.skillsProgress(
        currentAccount(req).id,
        req.params.childId as string,
        Number(req.query.days),
      ),
    );
  }),
);

dashboardRouter.get(
  '/children/:childId/activity',
  validateQuery(activityQuerySchema),
  asyncHandler(async (req, res) => {
    res.json({
      activity: await dashboardService.activity(
        currentAccount(req).id,
        req.params.childId as string,
        Number(req.query.take),
      ),
    });
  }),
);

dashboardRouter.get(
  '/children/:childId/insights',
  asyncHandler(async (req, res) => {
    res.json(await dashboardService.insights(currentAccount(req).id, req.params.childId as string));
  }),
);

// --- assistant --------------------------------------------------------------

dashboardRouter.get(
  '/children/:childId/chat',
  asyncHandler(async (req, res) => {
    res.json({
      messages: await chatService.history(currentAccount(req).id, req.params.childId as string),
    });
  }),
);

dashboardRouter.post(
  '/children/:childId/chat',
  validateBody(chatSendSchema),
  asyncHandler(async (req, res) => {
    res.status(201).json(
      await chatService.send(currentAccount(req).id, req.params.childId as string, req.body.text),
    );
  }),
);
