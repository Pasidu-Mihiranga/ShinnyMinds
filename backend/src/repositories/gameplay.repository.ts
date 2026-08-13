import type {
  AttemptStatus,
  Decision,
  GameSession,
  Mission,
  MissionAttempt,
  Skill,
} from '@prisma/client';
import { prisma } from '../lib/prisma.js';

/** Data access for the mission catalogue and everything the game records while playing. */
export const gameplayRepository = {
  // --- missions -------------------------------------------------------------

  listActiveMissions(): Promise<Mission[]> {
    return prisma.mission.findMany({
      where: { isActive: true },
      orderBy: { orderIndex: 'asc' },
    });
  },

  findMissionByCode(code: string): Promise<Mission | null> {
    return prisma.mission.findUnique({ where: { code } });
  },

  // --- sessions -------------------------------------------------------------

  createSession(data: { childId: string; platform?: string | null }): Promise<GameSession> {
    return prisma.gameSession.create({ data });
  },

  findSession(id: string): Promise<GameSession | null> {
    return prisma.gameSession.findUnique({ where: { id } });
  },

  updateSessionDuration(id: string, durationSeconds: number): Promise<GameSession> {
    return prisma.gameSession.update({
      where: { id },
      data: { durationSeconds },
    });
  },

  endSession(id: string, durationSeconds: number): Promise<GameSession> {
    return prisma.gameSession.update({
      where: { id },
      data: { durationSeconds, endedAt: new Date() },
    });
  },

  totalPlaytimeSeconds(childId: string, since?: Date): Promise<number> {
    return prisma.gameSession
      .aggregate({
        where: { childId, ...(since ? { startedAt: { gte: since } } : {}) },
        _sum: { durationSeconds: true },
      })
      .then((result) => result._sum.durationSeconds ?? 0);
  },

  listSessions(childId: string, since: Date): Promise<GameSession[]> {
    return prisma.gameSession.findMany({
      where: { childId, startedAt: { gte: since } },
      orderBy: { startedAt: 'asc' },
    });
  },

  // --- attempts -------------------------------------------------------------

  createAttempt(data: {
    childId: string;
    missionId: string;
    sessionId?: string | null;
    maxScore: number;
  }): Promise<MissionAttempt> {
    return prisma.missionAttempt.create({ data });
  },

  findAttempt(id: string): Promise<(MissionAttempt & { mission: Mission }) | null> {
    return prisma.missionAttempt.findUnique({
      where: { id },
      include: { mission: true },
    });
  },

  findOpenAttempt(childId: string, missionId: string): Promise<MissionAttempt | null> {
    return prisma.missionAttempt.findFirst({
      where: { childId, missionId, status: 'IN_PROGRESS' },
      orderBy: { startedAt: 'desc' },
    });
  },

  /** Moves an attempt's resume point. Called as the player passes each checkpoint. */
  saveCheckpoint(id: string, checkpointNodeId: string): Promise<MissionAttempt> {
    return prisma.missionAttempt.update({
      where: { id },
      data: { checkpointNodeId },
    });
  },

  completeAttempt(
    id: string,
    data: { status: AttemptStatus; score: number; durationSeconds: number },
  ): Promise<MissionAttempt> {
    return prisma.missionAttempt.update({
      where: { id },
      data: { ...data, completedAt: new Date() },
    });
  },

  listAttempts(
    childId: string,
    options: { since?: Date; take?: number } = {},
  ): Promise<(MissionAttempt & { mission: Mission })[]> {
    return prisma.missionAttempt.findMany({
      where: {
        childId,
        ...(options.since ? { startedAt: { gte: options.since } } : {}),
      },
      include: { mission: true },
      orderBy: { startedAt: 'desc' },
      ...(options.take ? { take: options.take } : {}),
    });
  },

  /** Best completed score per mission, used to build the mission-select screen. */
  bestScoresByMission(childId: string): Promise<Map<string, number>> {
    return prisma.missionAttempt
      .groupBy({
        by: ['missionId'],
        where: { childId, status: 'COMPLETED' },
        _max: { score: true },
      })
      .then(
        (rows) =>
          new Map(
            rows
              .filter((row) => row._max.score !== null)
              .map((row) => [row.missionId, row._max.score as number]),
          ),
      );
  },

  /** The most recently touched mission, so the menu's Continue button knows where to go. */
  findLatestAttempt(childId: string): Promise<(MissionAttempt & { mission: Mission }) | null> {
    return prisma.missionAttempt.findFirst({
      where: { childId },
      include: { mission: true },
      orderBy: { startedAt: 'desc' },
    });
  },

  /** Wipes a child's progress. Used by New Game. */
  async resetProgress(childId: string): Promise<void> {
    await prisma.$transaction([
      prisma.decision.deleteMany({ where: { childId } }),
      prisma.missionAttempt.deleteMany({ where: { childId } }),
      prisma.skillSnapshot.deleteMany({ where: { childId } }),
      prisma.gameSession.deleteMany({ where: { childId } }),
    ]);
  },

  // --- decisions ------------------------------------------------------------

  createDecision(data: {
    attemptId: string;
    childId: string;
    promptCode: string;
    promptText: string;
    choiceText: string;
    skill: Skill;
    isCorrect: boolean;
    scoreDelta: number;
  }): Promise<Decision> {
    return prisma.decision.create({ data });
  },

  listDecisions(childId: string, since?: Date): Promise<Decision[]> {
    return prisma.decision.findMany({
      where: { childId, ...(since ? { decidedAt: { gte: since } } : {}) },
      orderBy: { decidedAt: 'asc' },
    });
  },

  listDecisionsForAttempt(attemptId: string): Promise<Decision[]> {
    return prisma.decision.findMany({
      where: { attemptId },
      orderBy: { decidedAt: 'asc' },
    });
  },
};
