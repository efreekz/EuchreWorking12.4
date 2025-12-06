# RESTORE POINT – December 5, 2025
## Post Timeout Increase & Reconnection Planning

Use this snapshot to roll back to the state immediately after extending the Fusion timeout, fixing the Supabase singleton warning, and adding the reconnection plan plus dealer popup polish.

---

## 🔑 Key Changes Captured
1. **Dealer Trump Messaging** – `Assets/Scripts/Controllers/GamePlayControllerNetworked.cs`
   - Dealer now sees "It Lives"/"It Lives Alone" banners while discarding; non-dealer callers keep "Order Up" text.
2. **Supabase Token Manager Cleanup** – `Assets/Scripts/Network/SupabaseTokenManager.cs`
   - Singleton lookup switched to `FindFirstObjectByType` to remove CS0618 warnings.
3. **Reconnection Plan Doc** – `RECONNECTION_PLAN.md`
   - Phased approach for persisting session intent, server seat restoration, and UX handling.
4. **Fusion Timeout Bump** – `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`
   - `ConnectionTimeout` increased from 10s → 20s.
5. **Daily Session Notes** – `SESSION_NOTES_DEC_05_2025.md`
   - Detailed summary of today’s findings and completed work.

---

## ✅ Verification Status
- Unity compiles cleanly (no CS0618 warnings from SupabaseTokenManager).
- Manual editor check confirms dealer messages persist during discard.
- Fusion config reloads successfully after timeout change.

_No automated playtests were run beyond local editor sanity checks._

---

## 📂 Files Modified Since Previous Restore Point
```
Assets/Scripts/Controllers/GamePlayControllerNetworked.cs
Assets/Scripts/Network/SupabaseTokenManager.cs
Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion
RECONNECTION_PLAN.md
SESSION_NOTES_DEC_05_2025.md
```

---

## 🔁 Reverting Instructions
1. Ensure any working changes are stashed or committed.
2. Restore listed files to the versions recorded on Dec 5, 2025 (e.g., via Git checkout or file history).
3. Rebuild the project to confirm no merge conflicts or compiler errors.
4. Validate gameplay to ensure trump messaging, token manager behavior, and timeout values match expectations.

---

## 📝 Notes
- Fusion timeout now gives players ~20s before disconnect; further changes should update this restore point.
- The reconnection plan is documentation only—no runtime reconnect logic yet.
- If new reconnection code is added later, treat this file as the baseline for pre-implementation behavior.
