/**
 * Response shapes returned by the ShinyMinds API.
 *
 * These mirror the read models in backend/src/services/dashboard.service.ts. When an
 * endpoint changes, change it there first and update these to match - the backend is
 * the single source of truth for what a score or a status means.
 */

export type SkillKey = 'SAFETY' | 'COMMUNICATION' | 'EMPATHY' | 'CONFIDENCE';

export interface Parent {
  id: string;
  email: string;
  displayName: string;
  /** Six-character code the child enters in the game to link their account. */
  linkCode: string;
}

export interface Child {
  id: string;
  username: string;
  displayName: string;
  age: number | null;
  avatarUrl: string | null;
  isLinkedToParent: boolean;
}

export interface ChildWithScore extends Child {
  overallScore: number;
}

export interface Tokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: string;
}

export interface AuthResponse {
  parent: Parent;
  tokens: Tokens;
}

export interface SkillCard {
  key: SkillKey;
  label: string;
  score: number;
  color: string;
  icon: string;
  note: string;
}

export interface Overview {
  child: Child;
  overallWellbeing: { score: number; label: string };
  skills: SkillCard[];
  weekSummary: {
    range: string;
    scenariosCompleted: number;
    skillsPracticed: number;
    screenTimeTodayMin: number;
    screenTimePerDayMin: number;
  };
  aiTip: string;
}

export interface SkillsProgress {
  days: string[];
  series: {
    key: SkillKey;
    label: string;
    color: string;
    /** Null on days before the child had recorded any decisions. */
    values: (number | null)[];
    current: number;
  }[];
}

export interface ActivityItem {
  id: string;
  title: string;
  missionCode: string;
  focus: string;
  skill: SkillKey;
  color: string;
  status: 'Completed' | 'In Progress';
  score: number | null;
  maxScore: number;
  /** ISO timestamp. */
  when: string;
}

export interface Insights {
  weeklyCompletions: {
    total: number;
    breakdown: { key: SkillKey; label: string; count: number; pct: number; color: string }[];
  };
  needsAttention: SkillCard[];
}

export interface ChatMessage {
  id: string;
  from: 'ai' | 'parent';
  text: string;
  createdAt: string;
}

export interface ChatSendResponse {
  parentMessage: ChatMessage;
  aiMessage: ChatMessage;
}
