import { PagedResult } from './profile.models';

export type { PagedResult };

/** Which of a team's polls to list (feature 062). */
export type TeamPollState = 'open' | 'closed';

/** A member as a poll names them: a voter under an option, or someone who has not answered yet. */
export interface TeamPollPerson {
  name: string | null;
  handle: string | null;
}

/**
 * One option of a poll. `count` and `voters` are null whenever the server decided the caller may
 * not see the result (hidden until they answer), and `voters` is always null in an anonymous poll
 * — the server never sends who chose what there, so nothing here can leak it (FR-018).
 */
export interface TeamPollOption {
  id: string;
  text: string;
  count: number | null;
  voters: TeamPollPerson[] | null;
}

/** A team poll as the server built it for the signed-in member (feature 062). */
export interface TeamPoll {
  id: string;
  question: string;
  allowsMultiple: boolean;
  isAnonymous: boolean;
  resultsAfterAnswer: boolean;
  createdDate: string;
  /** Scheduled close; null when none was set. */
  closesAt: string | null;
  /** The moment it closed; null while open. */
  closedAt: string | null;
  isOpen: boolean;
  /** Null once the author is banned or deleted their account — render the former-player placeholder. */
  authorName: string | null;
  authorHandle: string | null;
  memberCount: number;
  answeredCount: number;
  resultsVisible: boolean;
  /** Anyone has answered: the question, options and settings can no longer change (FR-023). */
  hasAnswers: boolean;
  myOptionIds: string[];
  options: TeamPollOption[];
  /** Only for admins, only in named polls; otherwise null. */
  notAnswered: TeamPollPerson[] | null;
}

export interface CreateTeamPoll {
  question: string;
  options: string[];
  allowsMultiple: boolean;
  isAnonymous: boolean;
  resultsAfterAnswer: boolean;
  /** An ISO instant, or null for no close time. */
  closesAt: string | null;
}

/** The create request minus `isAnonymous`: whether a poll is anonymous can never change (FR-016). */
export type UpdateTeamPoll = Omit<CreateTeamPoll, 'isAnonymous'>;

/** The machine-readable reasons the server refuses a poll request (`extensions.code`). */
export type TeamPollErrorCode =
  | 'question'
  | 'optionCount'
  | 'optionLength'
  | 'optionDuplicate'
  | 'closesAtPast'
  | 'closesAtTooFar'
  | 'choiceCount'
  | 'choiceUnknown'
  | 'tooManyOpen'
  | 'closed'
  | 'answered';
