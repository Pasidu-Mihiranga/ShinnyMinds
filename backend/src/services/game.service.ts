import type { Skill } from '@prisma/client';
import { HttpError } from '../lib/http-error.js';
import { gameplayRepository } from '../repositories/gameplay.repository.js';
import { authService } from './auth.service.js';
import { progressService } from './progress.service.js';
import { SKILL_LABELS } from '../domain/skills.js';

/**
 * Everything the Unity client needs: what to show on the main menu, and how to record
 * what happened while playing.
 */
export const gameService = {
  /** Drives the main menu: who is signed in, whether Continue is available, and totals. */
  async profile(childId: string) {
    const [child, latestAttempt, playtimeSeconds, scores, missions] = await Promise.all([
      authService.childById(childId),
      gameplayRepository.findLatestAttempt(childId),
      gameplayRepository.totalPlaytimeSeconds(childId),
      progressService.currentScores(childId),
      gameplayRepository.listActiveMissions(),
    ]);

    const bestScores = await gameplayRepository.bestScoresByMission(childId);

    const completedCount = missions.filter((mission) => bestScores.has(mission.id)).length;

    return {
      child,
      playtimeSeconds,
      skills: scores,
      missionsCompleted: completedCount,
      missionsTotal: missions.length,
      hasSavedProgress: latestAttempt !== null,
      // Continue resumes an unfinished mission, or offers the next unplayed one.
      continueMission: resolveContinueTarget(missions, bestScores, latestAttempt),
    };
  },

  /** The mission-select screen: catalogue plus this child's status for each entry. */
  async missions(childId: string) {
    const [missions, bestScores, latestAttempt] = await Promise.all([
      gameplayRepository.listActiveMissions(),
      gameplayRepository.bestScoresByMission(childId),
      gameplayRepository.findLatestAttempt(childId),
    ]);

    // Missions unlock in order: the first incomplete one is playable, later ones are locked.
    let unlocked = true;

    return missions.map((mission) => {
      const bestScore = bestScores.get(mission.id) ?? null;
      const isCompleted = bestScore !== null;
      const isUnlocked = unlocked;

      if (!isCompleted) {
        unlocked = false;
      }

      return {
        code: mission.code,
        title: mission.title,
        description: mission.description,
        topic: mission.topic,
        skill: mission.skill,
        skillLabel: SKILL_LABELS[mission.skill],
        maxScore: mission.maxScore,
        orderIndex: mission.orderIndex,
        bestScore,
        isCompleted,
        isUnlocked,
        isInProgress:
          latestAttempt?.missionId === mission.id && latestAttempt.status === 'IN_PROGRESS',
      };
    });
  },

  async startSession(childId: string, platform?: string) {
    const session = await gameplayRepository.createSession({ childId, platform: platform ?? null });

    return { sessionId: session.id, startedAt: session.startedAt };
  },

  /**
   * Unity posts elapsed playtime periodically. The value is treated as the total for the
   * session rather than a delta, so a dropped heartbeat does not lose time and a repeated
   * one does not double-count it.
   */
  async heartbeat(childId: string, sessionId: string, durationSeconds: number) {
    await this.assertSessionOwned(childId, sessionId);

    const session = await gameplayRepository.updateSessionDuration(sessionId, durationSeconds);

    return { sessionId: session.id, durationSeconds: session.durationSeconds };
  },

  async endSession(childId: string, sessionId: string, durationSeconds: number) {
    await this.assertSessionOwned(childId, sessionId);

    const session = await gameplayRepository.endSession(sessionId, durationSeconds);

    return {
      sessionId: session.id,
      durationSeconds: session.durationSeconds,
      endedAt: session.endedAt,
    };
  },

  async startMission(childId: string, missionCode: string, sessionId?: string) {
    const mission = await gameplayRepository.findMissionByCode(missionCode);

    if (!mission || !mission.isActive) {
      throw HttpError.notFound(`No active mission with code "${missionCode}".`);
    }

    if (sessionId) {
      await this.assertSessionOwned(childId, sessionId);
    }

    // Resuming rather than starting a second attempt keeps "in progress" meaningful
    // when a player quits mid-mission and comes back.
    const existing = await gameplayRepository.findOpenAttempt(childId, mission.id);

    if (existing) {
      return { attemptId: existing.id, missionCode: mission.code, resumed: true };
    }

    const attempt = await gameplayRepository.createAttempt({
      childId,
      missionId: mission.id,
      sessionId: sessionId ?? null,
      maxScore: mission.maxScore,
    });

    return { attemptId: attempt.id, missionCode: mission.code, resumed: false };
  },

  async recordDecision(
    childId: string,
    attemptId: string,
    input: {
      promptCode: string;
      promptText: string;
      choiceText: string;
      skill: Skill;
      isCorrect: boolean;
      scoreDelta?: number;
    },
  ) {
    const attempt = await this.assertAttemptOwned(childId, attemptId);

    if (attempt.status !== 'IN_PROGRESS') {
      throw HttpError.conflict('That mission attempt has already finished.');
    }

    const decision = await gameplayRepository.createDecision({
      attemptId,
      childId,
      promptCode: input.promptCode,
      promptText: input.promptText,
      choiceText: input.choiceText,
      skill: input.skill,
      isCorrect: input.isCorrect,
      scoreDelta: input.scoreDelta ?? (input.isCorrect ? 10 : 0),
    });

    const skills = await progressService.recordSnapshot(childId);

    return { decisionId: decision.id, skills };
  },

  async completeMission(
    childId: string,
    attemptId: string,
    input: { durationSeconds: number; abandoned?: boolean },
  ) {
    const attempt = await this.assertAttemptOwned(childId, attemptId);

    if (attempt.status !== 'IN_PROGRESS') {
      throw HttpError.conflict('That mission attempt has already finished.');
    }

    const decisions = await gameplayRepository.listDecisionsForAttempt(attemptId);

    // Marks are the sum of what each choice was worth, clamped to the mission's maximum.
    const rawScore = decisions.reduce((sum, decision) => sum + decision.scoreDelta, 0);
    const score = Math.max(0, Math.min(attempt.maxScore, rawScore));

    const completed = await gameplayRepository.completeAttempt(attemptId, {
      status: input.abandoned ? 'ABANDONED' : 'COMPLETED',
      score,
      durationSeconds: input.durationSeconds,
    });

    const skills = await progressService.recordSnapshot(childId);

    return {
      attemptId: completed.id,
      missionCode: attempt.mission.code,
      status: completed.status,
      score,
      maxScore: attempt.maxScore,
      decisionsRecorded: decisions.length,
      skills,
    };
  },

  /** New Game: clears saved progress so the child starts from mission one. */
  async newGame(childId: string) {
    await gameplayRepository.resetProgress(childId);

    return { reset: true };
  },

  // --- ownership guards -----------------------------------------------------
  // Every id in a request body is attacker-controlled, so each one is checked
  // against the signed-in child before it is used.

  async assertSessionOwned(childId: string, sessionId: string) {
    const session = await gameplayRepository.findSession(sessionId);

    if (!session || session.childId !== childId) {
      throw HttpError.notFound('Session not found.');
    }

    return session;
  },

  async assertAttemptOwned(childId: string, attemptId: string) {
    const attempt = await gameplayRepository.findAttempt(attemptId);

    if (!attempt || attempt.childId !== childId) {
      throw HttpError.notFound('Mission attempt not found.');
    }

    return attempt;
  },
};

function resolveContinueTarget(
  missions: { id: string; code: string; title: string; topic: string }[],
  bestScores: Map<string, number>,
  latestAttempt: { missionId: string; status: string; mission: { code: string; title: string; topic: string } } | null,
) {
  if (latestAttempt && latestAttempt.status === 'IN_PROGRESS') {
    return {
      code: latestAttempt.mission.code,
      title: latestAttempt.mission.title,
      topic: latestAttempt.mission.topic,
      resuming: true,
    };
  }

  const nextUnplayed = missions.find((mission) => !bestScores.has(mission.id));

  if (!nextUnplayed) {
    return null;
  }

  return {
    code: nextUnplayed.code,
    title: nextUnplayed.title,
    topic: nextUnplayed.topic,
    resuming: false,
  };
}
