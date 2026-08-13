import { Router } from 'express';
import { asyncHandler } from '../lib/async-handler.js';
import { authenticate, currentAccount } from '../middleware/auth.js';
import { validateBody } from '../middleware/validate.js';
import {
  completeMissionSchema,
  decisionSchema,
  sessionProgressSchema,
  startMissionSchema,
  startSessionSchema,
} from '../dto/index.js';
import { gameService } from '../services/game.service.js';

/**
 * Called by the Unity client. Every route requires a child token, so a mission result
 * can only ever be written against the player who is actually signed in.
 */
export const gameRouter = Router();

gameRouter.use(authenticate('child'));

gameRouter.get(
  '/profile',
  asyncHandler(async (req, res) => {
    res.json(await gameService.profile(currentAccount(req).id));
  }),
);

gameRouter.get(
  '/missions',
  asyncHandler(async (req, res) => {
    res.json({ missions: await gameService.missions(currentAccount(req).id) });
  }),
);

gameRouter.post(
  '/sessions',
  validateBody(startSessionSchema),
  asyncHandler(async (req, res) => {
    res.status(201).json(await gameService.startSession(currentAccount(req).id, req.body.platform));
  }),
);

gameRouter.post(
  '/sessions/:sessionId/heartbeat',
  validateBody(sessionProgressSchema),
  asyncHandler(async (req, res) => {
    res.json(
      await gameService.heartbeat(
        currentAccount(req).id,
        req.params.sessionId as string,
        req.body.durationSeconds,
      ),
    );
  }),
);

gameRouter.post(
  '/sessions/:sessionId/end',
  validateBody(sessionProgressSchema),
  asyncHandler(async (req, res) => {
    res.json(
      await gameService.endSession(
        currentAccount(req).id,
        req.params.sessionId as string,
        req.body.durationSeconds,
      ),
    );
  }),
);

gameRouter.post(
  '/missions/start',
  validateBody(startMissionSchema),
  asyncHandler(async (req, res) => {
    res.status(201).json(
      await gameService.startMission(
        currentAccount(req).id,
        req.body.missionCode,
        req.body.sessionId,
      ),
    );
  }),
);

gameRouter.post(
  '/attempts/:attemptId/decisions',
  validateBody(decisionSchema),
  asyncHandler(async (req, res) => {
    res.status(201).json(
      await gameService.recordDecision(
        currentAccount(req).id,
        req.params.attemptId as string,
        req.body,
      ),
    );
  }),
);

gameRouter.post(
  '/attempts/:attemptId/complete',
  validateBody(completeMissionSchema),
  asyncHandler(async (req, res) => {
    res.json(
      await gameService.completeMission(
        currentAccount(req).id,
        req.params.attemptId as string,
        req.body,
      ),
    );
  }),
);

// New Game on the main menu. Destructive, so it is an explicit endpoint rather than
// a side effect of starting the first mission.
gameRouter.post(
  '/new-game',
  asyncHandler(async (req, res) => {
    res.json(await gameService.newGame(currentAccount(req).id));
  }),
);
