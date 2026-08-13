import { PrismaClient, Skill } from '@prisma/client';

const prisma = new PrismaClient();

/**
 * The mission catalogue.
 *
 * `topic` is the exact string the Unity GroqDialogue component uses to steer NPC
 * dialogue, so adding a mission here makes it playable without touching game code.
 */
const missions = [
  {
    code: 'road_crossing',
    title: 'Crossing the Road',
    description: 'Learn to stop, look both ways and use a zebra crossing safely.',
    topic: 'road crossing safety',
    skill: Skill.SAFETY,
    orderIndex: 1,
    maxScore: 100,
  },
  {
    code: 'school_zone',
    title: 'The Road Home',
    description: 'Walk home from school safely, following the crossing guard and staying on the pavement.',
    topic: 'school zone safety',
    skill: Skill.SAFETY,
    orderIndex: 2,
    maxScore: 100,
  },
  {
    code: 'traffic_lights',
    title: 'Reading the Signals',
    description: 'Understand traffic lights and pedestrian signals before stepping out.',
    topic: 'traffic light safety',
    skill: Skill.SAFETY,
    orderIndex: 3,
    maxScore: 100,
  },
  {
    code: 'bus_stop',
    title: 'Waiting for the Bus',
    description: 'Wait behind the line and board the school bus safely.',
    topic: 'bus stop safety',
    skill: Skill.SAFETY,
    orderIndex: 4,
    maxScore: 100,
  },
  {
    code: 'new_classmate',
    title: 'The New Classmate',
    description: 'Introduce yourself to someone new and make them feel welcome.',
    topic: 'making friends and introductions',
    skill: Skill.CONFIDENCE,
    orderIndex: 5,
    maxScore: 100,
  },
  {
    code: 'helping_friend',
    title: 'Helping a Friend',
    description: 'Notice when a friend is upset and find a kind way to help.',
    topic: 'empathy and helping others',
    skill: Skill.EMPATHY,
    orderIndex: 6,
    maxScore: 100,
  },
  {
    code: 'saying_sorry',
    title: 'Saying Sorry',
    description: 'Practise apologising honestly and repairing a friendship.',
    topic: 'apologising and resolving conflict',
    skill: Skill.COMMUNICATION,
    orderIndex: 7,
    maxScore: 100,
  },
  {
    code: 'strangers_gift',
    title: "The Stranger's Gift",
    description: 'Recognise unsafe situations with strangers and tell a trusted adult.',
    topic: 'stranger safety',
    skill: Skill.SAFETY,
    orderIndex: 8,
    maxScore: 100,
  },
];

async function main() {
  for (const mission of missions) {
    // Upsert so re-running the seed updates copy without wiping anybody's attempts.
    await prisma.mission.upsert({
      where: { code: mission.code },
      update: mission,
      create: mission,
    });
  }

  console.log(`Seeded ${missions.length} missions.`);
}

main()
  .catch((error) => {
    console.error(error);
    process.exit(1);
  })
  .finally(() => prisma.$disconnect());
