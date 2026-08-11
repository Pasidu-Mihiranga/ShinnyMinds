import type { ChatMessage, ChatRole, Skill, SkillSnapshot } from '@prisma/client';
import { prisma } from '../lib/prisma.js';
import { startOfUtcDay } from '../lib/dates.js';

/** Data access for the daily skill history and the parent assistant transcript. */
export const analyticsRepository = {
  listSnapshots(childId: string, since: Date): Promise<SkillSnapshot[]> {
    return prisma.skillSnapshot.findMany({
      where: { childId, day: { gte: startOfUtcDay(since) } },
      orderBy: { day: 'asc' },
    });
  },

  /**
   * Writes today's score for one skill. Upsert rather than insert so a child who
   * plays several times a day ends with one row per day, not one row per session.
   */
  upsertSnapshot(childId: string, skill: Skill, score: number, day = new Date()) {
    const normalisedDay = startOfUtcDay(day);

    return prisma.skillSnapshot.upsert({
      where: { childId_skill_day: { childId, skill, day: normalisedDay } },
      update: { score },
      create: { childId, skill, score, day: normalisedDay },
    });
  },

  listChatMessages(parentId: string, childId: string, take = 50): Promise<ChatMessage[]> {
    return prisma.chatMessage
      .findMany({
        where: { parentId, childId },
        orderBy: { createdAt: 'desc' },
        take,
      })
      .then((rows) => rows.reverse());
  },

  createChatMessage(data: {
    parentId: string;
    childId: string;
    role: ChatRole;
    content: string;
  }): Promise<ChatMessage> {
    return prisma.chatMessage.create({ data });
  },
};
