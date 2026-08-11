import type { ChatMessage, Skill, SkillSnapshot } from '@prisma/client';
import { prisma } from '../lib/prisma.js';
import { localDayKey } from '../lib/dates.js';

/** Data access for the daily skill history and the parent assistant transcript. */
export const analyticsRepository = {
  listSnapshots(childId: string, sinceDayKey: Date): Promise<SkillSnapshot[]> {
    return prisma.skillSnapshot.findMany({
      where: { childId, day: { gte: sinceDayKey } },
      orderBy: { day: 'asc' },
    });
  },

  /**
   * Writes today's score for one skill. Upsert rather than insert so a child who
   * plays several times a day ends with one row per day, not one row per session.
   */
  upsertSnapshot(childId: string, skill: Skill, score: number, day = localDayKey(new Date())) {
    return prisma.skillSnapshot.upsert({
      where: { childId_skill_day: { childId, skill, day } },
      update: { score },
      create: { childId, skill, score, day },
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

  /**
   * Stores a question and its answer together.
   *
   * Written as one transaction so a failure cannot leave the parent's message in the
   * transcript with nothing answering it. The timestamps are set explicitly and one
   * millisecond apart: PostgreSQL's now() is the transaction start time, so both rows
   * would otherwise share an identical createdAt and could come back in either order.
   */
  async createExchange(data: {
    parentId: string;
    childId: string;
    parentText: string;
    aiText: string;
  }): Promise<{ parentMessage: ChatMessage; aiMessage: ChatMessage }> {
    const askedAt = new Date();
    const answeredAt = new Date(askedAt.getTime() + 1);

    const [parentMessage, aiMessage] = await prisma.$transaction([
      prisma.chatMessage.create({
        data: {
          parentId: data.parentId,
          childId: data.childId,
          role: 'PARENT',
          content: data.parentText,
          createdAt: askedAt,
        },
      }),
      prisma.chatMessage.create({
        data: {
          parentId: data.parentId,
          childId: data.childId,
          role: 'AI',
          content: data.aiText,
          createdAt: answeredAt,
        },
      }),
    ]);

    return { parentMessage, aiMessage };
  },
};
