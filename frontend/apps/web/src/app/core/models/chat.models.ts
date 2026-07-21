/**
 * Client shapes for chat (feature 019). Mirrors backend/Dtos/Chat/ChatDtos.cs; enums arrive as their
 * names (global JsonStringEnumConverter), so these are string unions rather than numbers.
 */

/**
 * The discriminator that drives everything conditional in chat: how the name is derived, whether the
 * conversation can be left or added to, and which tag the inbox row wears.
 */
export type ConversationKind = 'Direct' | 'Group' | 'Team' | 'Party';

/** Archived = the underlying party disbanded or team was deleted: readable, closed to writes. */
export type ConversationState = 'Active' | 'Archived';

export type ChatMessageKind = 'Member' | 'System';

export type ChatSystemEvent = 'Joined' | 'Left' | 'Removed' | 'GroupCreated';

export type ChatLinkKind = 'None' | 'Player' | 'Team' | 'Event' | 'Training';

/** Delivery state shown under your own messages in a DM. Null on other people's. */
export type ReadState = 'Sent' | 'Read' | null;

export interface ConversationAvatar {
  readonly kind: string;
  readonly userId: string | null;
  readonly teamId: string | null;
  readonly url: string | null;
}

export interface LastMessage {
  /** Empty when the newest message was deleted — a tombstone shows no preview. */
  readonly preview: string;
  readonly at: string;
  readonly senderName: string | null;
  readonly isOwn: boolean;
  readonly isSystem: boolean;
}

export interface Conversation {
  readonly id: string;
  readonly kind: ConversationKind;
  readonly name: string;
  readonly avatar: ConversationAvatar;
  readonly lastMessage: LastMessage | null;
  readonly unreadCount: number;
  readonly isMuted: boolean;
  readonly state: ConversationState;
  readonly teamId: string | null;
  readonly partyId: string | null;
}

export interface ConversationDetail {
  readonly id: string;
  readonly kind: ConversationKind;
  readonly name: string;
  readonly avatar: ConversationAvatar;
  readonly state: ConversationState;
  readonly isMuted: boolean;
  readonly isHidden: boolean;
  readonly memberCount: number;
  /** Server-decided: only a manual group can be left. The UI must not offer what the server refuses. */
  readonly canLeave: boolean;
  readonly canAddMembers: boolean;
  readonly teamId: string | null;
  readonly partyId: string | null;
}

export interface ChatMember {
  readonly userId: string;
  readonly displayName: string;
  readonly handle: string | null;
  readonly avatarUrl: string | null;
  readonly isYou: boolean;
  readonly viaMarket: boolean;
}

/**
 * A view-only card for a linked JuggerHub item. Null on a message means "render the body's link as
 * plain text" — which covers no-link, a target this viewer may not see, and a deleted target, all
 * deliberately indistinguishable.
 */
export interface LinkCard {
  readonly kind: ChatLinkKind;
  readonly targetId: string;
  readonly title: string;
  readonly subtitle: string | null;
  readonly href: string;
  readonly avatarUrl: string | null;
}

export interface ChatMessage {
  readonly id: string;
  readonly kind: ChatMessageKind;
  readonly senderId: string | null;
  readonly senderName: string | null;
  readonly isOwn: boolean;
  readonly body: string;
  readonly sentAt: string;
  readonly isDeleted: boolean;
  readonly readState: ReadState;
  readonly systemEvent: ChatSystemEvent | null;
  readonly systemSubjectName: string | null;
  readonly linkCard: LinkCard | null;
}

/** A keyset page. `nextBefore` is the cursor for the next page back; null when history is exhausted. */
export interface MessagePage {
  readonly items: readonly ChatMessage[];
  readonly nextBefore: string | null;
}

export interface MessageSearchHit {
  readonly messageId: string;
  readonly conversationId: string;
  readonly conversationName: string;
  readonly conversationKind: ConversationKind;
  readonly snippet: string;
  readonly sentAt: string;
  readonly senderName: string | null;
}

export interface PersonHit {
  readonly userId: string;
  readonly displayName: string;
  readonly handle: string | null;
  readonly avatarUrl: string | null;
  /** Set when a DM already exists — open it rather than starting a duplicate. */
  readonly existingConversationId: string | null;
}

export interface ChatSearchResult {
  readonly messages: { items: readonly MessageSearchHit[]; totalCount: number };
  readonly people: { items: readonly PersonHit[]; totalCount: number };
}

/**
 * Result of sending the first message to a player (feature 022 — lazy DM creation): the direct
 * conversation that now exists (created if it didn't) plus the message that was sent.
 */
export interface DirectMessageSent {
  readonly conversationId: string;
  readonly message: ChatMessage;
}

export interface BlockedUser {
  readonly userId: string;
  readonly displayName: string;
  readonly handle: string | null;
  readonly blockedAt: string;
}

/** Someone typing right now. Ephemeral — it expires on a timer and is never loaded from the server. */
export interface TypingSignal {
  readonly conversationId: string;
  readonly userId: string;
  readonly displayName: string;
  readonly expiresAt: number;
}
