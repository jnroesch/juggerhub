import type { IRetryPolicy, RetryContext } from '@microsoft/signalr';

/** First reconnect is immediate — a momentary blip should recover invisibly. */
const FIRST_DELAY_MS = 0;
/** Growth starts here and doubles. */
const BASE_DELAY_MS = 2_000;
/** Ceiling, so a long outage settles into a steady, cheap poll rather than backing off forever. */
const MAX_DELAY_MS = 60_000;

/**
 * Reconnect policy shared by the chat and notification hubs (feature 028, FR-012).
 *
 * SignalR's parameterless `withAutomaticReconnect()` tries at 0s, 2s, 10s and 30s and then
 * **stops permanently**. That default is the wrong shape for JuggerHub: a rolling deploy routinely
 * exceeds ~42 seconds, after which the socket is dead until the page is reloaded — and because
 * live updates are a silent enhancement, nobody is told. Messages simply stop arriving on a screen
 * that looks fine.
 *
 * This policy keeps trying indefinitely, backing off to a {@link MAX_DELAY_MS} ceiling and
 * jittering each delay so every client stranded by the same deploy doesn't stampede the first pod
 * back up.
 *
 * Retrying forever is safe here *because* both services re-seed their state on `onreconnected` and
 * treat the socket as an enhancement over the REST path — a reconnect at minute ten is correct,
 * not just live.
 */
export class IndefiniteHubRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(context: RetryContext): number {
    if (context.previousRetryCount === 0) {
      return FIRST_DELAY_MS;
    }

    const growth = BASE_DELAY_MS * 2 ** (context.previousRetryCount - 1);
    const capped = Math.min(growth, MAX_DELAY_MS);

    // Jitter across the top half of the window: still promptly responsive, but spread out.
    return capped / 2 + Math.random() * (capped / 2);
  }
}
