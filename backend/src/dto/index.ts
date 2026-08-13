import { z } from 'zod';

/**
 * Request schemas, kept together so the shape of every payload the API accepts can be
 * read in one place - the Unity client and the dashboard are both written against these.
 */

const password = z
  .string()
  .min(8, 'Password must be at least 8 characters.')
  .max(128, 'Password must be at most 128 characters.');

const skill = z.enum(['SAFETY', 'COMMUNICATION', 'EMPATHY', 'CONFIDENCE']);

// --- auth -------------------------------------------------------------------

export const parentRegisterSchema = z.object({
  email: z.string().email('Enter a valid email address.'),
  password,
  displayName: z.string().min(1, 'Please enter your name.').max(80),
});

export const parentLoginSchema = z.object({
  email: z.string().email('Enter a valid email address.'),
  password: z.string().min(1, 'Please enter your password.'),
});

export const childRegisterSchema = z.object({
  username: z
    .string()
    .min(3, 'Username must be at least 3 characters.')
    .max(24, 'Username must be at most 24 characters.')
    .regex(/^[a-zA-Z0-9_]+$/, 'Username may contain letters, numbers and underscores only.'),
  password,
  displayName: z.string().min(1, 'Please enter a display name.').max(40),
  age: z.coerce.number().int().min(5).max(18).optional(),
  parentLinkCode: z.string().length(6, 'A parent code is 6 characters.').optional(),
});

export const childLoginSchema = z.object({
  username: z.string().min(1, 'Please enter your username.'),
  password: z.string().min(1, 'Please enter your password.'),
});

export const refreshSchema = z.object({
  refreshToken: z.string().min(1, 'A refresh token is required.'),
});

export const linkParentSchema = z.object({
  parentLinkCode: z.string().length(6, 'A parent code is 6 characters.'),
});

// --- gameplay ---------------------------------------------------------------

export const startSessionSchema = z.object({
  platform: z.string().max(80).optional(),
});

export const sessionProgressSchema = z.object({
  durationSeconds: z.coerce.number().int().min(0).max(86_400),
});

export const startMissionSchema = z.object({
  missionCode: z.string().min(1, 'A mission code is required.'),
  sessionId: z.string().uuid().optional(),
});

export const decisionSchema = z.object({
  promptCode: z.string().min(1).max(120),
  promptText: z.string().min(1).max(1000),
  choiceText: z.string().min(1).max(1000),
  skill,
  isCorrect: z.boolean(),
  // Optional: defaults to 10 for a correct choice and 0 otherwise.
  scoreDelta: z.coerce.number().int().min(-100).max(100).optional(),
});

// Node ids come from the mission asset, e.g. "s2_crossing_choice".
export const checkpointSchema = z.object({
  nodeId: z.string().min(1).max(120),
});

export const completeMissionSchema = z.object({
  durationSeconds: z.coerce.number().int().min(0).max(86_400),
  abandoned: z.boolean().optional(),
});

// --- dashboard --------------------------------------------------------------

export const skillsProgressQuerySchema = z.object({
  days: z.coerce.number().int().min(1).max(90).default(7),
});

export const activityQuerySchema = z.object({
  take: z.coerce.number().int().min(1).max(50).default(10),
});

export const chatSendSchema = z.object({
  text: z.string().min(1, 'Please type a message.').max(2000, 'Message is too long.'),
});

export type ParentRegisterInput = z.infer<typeof parentRegisterSchema>;
export type ChildRegisterInput = z.infer<typeof childRegisterSchema>;
export type DecisionInput = z.infer<typeof decisionSchema>;
