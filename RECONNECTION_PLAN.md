# Reconnection & Resilience Plan

## Goals
- Let a disconnected player return to the same Euchre hand without forfeiting chips or progress.
- Keep the Fusion runner alive long enough to reuse Photon/Fusion reconnection tokens instead of dumping to the main menu immediately.
- Ensure Supabase state (entry fee, balance, auth) stays in sync when reconnecting or when a session ultimately fails.

## Constraints & Assumptions
- Current flow destroys the `NetworkRunner` and reloads the main menu as soon as `OnDisconnectedFromServer` fires.
- Seats are tracked only in-memory through `RPCManager.JoinedPlayers`; no persistent mapping exists between Supabase user IDs and seat indices.
- Bots may replace players when a seat empties, so a reconnecting player must eject (or temporarily pause) that bot cleanly.
- Photon Fusion provides reconnection tokens, but gameplay-specific state sync (cards in hand, trick progress, trump info) is our responsibility.

## Proposed Phases

### Phase 1 – Persist Client Session Intent
1. **Capture identifiers**
   - Store `SessionInfo` / lobby ID, player seat index, and latest `ReconnectionToken` in a `ReconnectState` struct.
   - Serialize to disk (PlayerPrefs or Supabase token storage) so it survives editor restarts.
2. **Runner lifecycle change**
   - On disconnect, keep the runner object alive and set a `Reconnecting` flag instead of shutting down.
   - Present a blocking UI (“Reconnecting… [Cancel]”).
3. **Retry logic**
   - Attempt Photon reconnection for N seconds using the stored token. If it fails, fall back to today’s behavior (return to main menu + refund chip logic if necessary).

### Phase 2 – Server Acceptance & Seat Restoration
1. **Authenticate reconnecting player**
   - Pass Supabase user ID with the reconnection RPC; host verifies it against the seat’s original owner.
2. **Seat reconciliation**
   - If a bot filled the seat, pause bot actions and remove it when the real player returns.
   - If the seat is already occupied by another human, reject the reconnect and notify the client.
3. **State transfer**
   - Add RPC to serialize current hand, tricks won, trump suit, dealer state, score, and pending prompts.
   - Upon reconnection, host sends this payload; client hydrates `PlayerBase` + UI with that state before resuming input.

### Phase 3 – UX & Edge Cases
1. **Toast & logging**
   - Clear messaging for reconnect attempts, failures, and success.
2. **Timeout handling**
   - Allow player to cancel reconnection (returns to lobby, forfeits seat, triggers fee handling if needed).
3. **Host migration / shutdown**
   - If the server crashed, reconnection should eventually surface “Game ended” rather than spinning forever.

## Implementation Notes
- **Data structures**: Introduce `ReconnectState` (client) and `PlayerSeatSnapshot` (server).
- **Photon hooks**: Use Fusion’s `ReconnectionToken` (via `StartGameArgs.Session` and `ReconnectKey`). Keep the runner active to reuse these tokens.
- **Supabase coordination**: On final failure, re-run the refund path so fees aren’t lost; on success, ensure no duplicate deductions occur.
- **Testing**: Simulate disconnects by killing the network adapter or pausing the host editor. Verify clients rejoin with the same cards and can play the current trick.

## Open Questions
- Should we pause the game (e.g., freeze timers) while waiting for a reconnect, or let the bot continue playing until the human returns?
- Do we want reconnect to persist across full app restarts, or is reconnect-within-1-minute sufficient?
- How many simultaneous reconnect attempts should we allow before we permanently replace the player with a bot?

## Next Steps
1. Approve phased approach and desired UX (auto vs manual retry, pause vs bot play).
2. Spike Phase 1 (runner lifecycle + reconnect token storage) behind a feature flag.
3. Design serialization format for Phase 2 and inventory the gameplay state we must resend.
