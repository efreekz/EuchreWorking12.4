# Bot MCTS Threading Implementation Guide
**Created:** December 1, 2025  
**Status:** Planning Document  
**For:** Future implementation after quick fix validation

---

## 🎯 OBJECTIVE

Move bot MCTS simulation from Unity main thread to background thread, eliminating Photon Fusion timeouts while maintaining or improving bot intelligence.

---

## 📊 CURRENT VS TARGET PERFORMANCE

### Current (Post-Quick Fix)
```
Simulation Count:  75
Main Thread Time:  2-3 seconds (with yields)
Max Block Time:    0.3 seconds between yields
Bot Intelligence:  Strong
Photon Timeout:    Should be fixed
Scalability:       Limited (can't increase simulations)
```

### Target (With Threading)
```
Simulation Count:  150-200 (2-3x smarter!)
Main Thread Time:  0ms (non-blocking)
Background Time:   2-4 seconds
Bot Intelligence:  Elite
Photon Timeout:    Impossible (main thread always free)
Scalability:       Excellent (can increase to 500+)
```

---

## 🔍 PRE-IMPLEMENTATION INVESTIGATION

### CRITICAL: Verify Thread Safety

#### 1. Check CardData Structure
**File:** `Assets/Scripts/GamePlay/Cards/CardData.cs`

**Questions to Answer:**
```csharp
// Is CardData thread-safe?
public class CardData : ??? // ScriptableObject? MonoBehaviour? Plain class?
{
    public Rank rank;          // Value type = OK
    public Suit suit;          // Value type = OK
    
    // Check for these red flags:
    // ❌ Inherits from ScriptableObject
    // ❌ Inherits from MonoBehaviour  
    // ❌ Has UnityEngine.Object references
    // ❌ Has static mutable state
    
    // If plain C# class with only value types = ✅ THREAD SAFE
}
```

**Action Items:**
- [ ] Open CardData.cs
- [ ] Check class inheritance
- [ ] Verify all fields are value types or immutable
- [ ] Document findings

**Expected Result:** CardData should be thread-safe (it's card data, likely plain struct/class)

---

#### 2. Audit Random Number Generation
**Search For:** All usage of `Random` in bot code

**Known Locations:**
```
- OnlineBot.cs: Random.Range(1200, 2400) for thinking delay
- ListExtension.cs: Likely has GetRandom() method
- GameSimulator.cs: SelectIntelligentSimulationCard might use random
```

**Problem:**
```csharp
// ❌ NOT THREAD SAFE:
UnityEngine.Random.Range(0, 10);

// ✅ THREAD SAFE:
System.Random threadRandom = new System.Random();
threadRandom.Next(0, 10);
```

**Solution Template:**
```csharp
public static class ThreadSafeRandom
{
    [ThreadStatic]
    private static System.Random _random;

    private static System.Random Random
    {
        get
        {
            if (_random == null)
            {
                // Use Guid for unique seed per thread
                _random = new System.Random(Guid.NewGuid().GetHashCode());
            }
            return _random;
        }
    }

    public static int Next(int min, int max)
    {
        return Random.Next(min, max);
    }

    public static T GetRandom<T>(this List<T> list)
    {
        if (list == null || list.Count == 0)
            return default;
        return list[Random.Next(0, list.Count)];
    }
}
```

**Action Items:**
- [ ] Find all Random usage in simulation code
- [ ] Create ThreadSafeRandom utility class
- [ ] Replace UnityEngine.Random with ThreadSafeRandom
- [ ] Test random distribution

---

#### 3. GameLogger Thread Safety
**Current Usage:** GameLogger.ShowLog() called throughout simulation

**Options:**

**Option A: Remove All Logging (RECOMMENDED)**
```csharp
// Keep #if UNITY_EDITOR wrappers from quick fix
// Clean and fast, no thread issues
#if UNITY_EDITOR && ENABLE_BOT_LOGGING
GameLogger.ShowLog("Message");
#endif
```

**Option B: Collect and Display Later**
```csharp
// In simulation (background thread):
private List<string> _logs = new List<string>();
_logs.Add("Message");

// On main thread after completion:
foreach (var log in _logs)
    GameLogger.ShowLog(log);
```

**Option C: Thread-Safe Logger**
```csharp
public class ThreadSafeLogger
{
    private ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
    
    public void Log(string message)
    {
        _logQueue.Enqueue(message);
    }
    
    public void FlushToUnity()
    {
        while (_logQueue.TryDequeue(out string message))
            Debug.Log(message);
    }
}
```

**Recommendation:** Option A (remove logging) - Fastest and safest

---

## 🏗️ IMPLEMENTATION ARCHITECTURE

### Current Call Stack
```
OnlineBot.PlayTurn() [MAIN THREAD]
    ↓
EuchreBotDecisionEngine.SelectCardToPlay() [MAIN THREAD - BLOCKS 2-3s]
    ↓
GameSimulator.SimulatePlayout() [MAIN THREAD - BLOCKS]
    ↓
Return CardData [MAIN THREAD]
```

### Target Call Stack
```
OnlineBot.PlayTurn() [MAIN THREAD]
    ↓
UniTask.RunOnThreadPool() [SPAWNS BACKGROUND THREAD]
    ↓                                           ↓
[MAIN THREAD - FREE]              [BACKGROUND THREAD]
    ↓                                           ↓
Render UI                         EuchreBotDecisionEngine.SelectCardToPlay()
Handle Input                                    ↓
Process Photon Packets            GameSimulator.SimulatePlayout()
    ↓                                           ↓
await thread completion           Return CardData
    ↓                                           ↓
Receive CardData ←────────────────────────────┘
    ↓
Play card [MAIN THREAD]
```

---

## 💻 IMPLEMENTATION CODE EXAMPLES

### Step 1: Make EuchreBotDecisionEngine Thread-Safe

**File:** `EuchreBotDecisionEngine.cs`

**Current Signature:**
```csharp
public async UniTask<CardData> SelectCardToPlay(GameState gameState)
```

**New Signature:**
```csharp
public CardData SelectCardToPlayThreaded(GameState gameState, CancellationToken cancellationToken = default)
```

**Why Remove async?**
- Background threads can't use Unity's async (yields to main thread)
- UniTask.RunOnThreadPool handles the async part
- Simulation code is pure synchronous logic

**Key Changes:**
```csharp
public CardData SelectCardToPlayThreaded(GameState gameState, CancellationToken cancellationToken = default)
{
    // Remove all GameLogger calls (already done in quick fix)
    // Remove all UniTask.Yield() calls (not needed on background thread)
    // Add cancellation checks
    
    for (var i = 0; i < _count; i++)
    {
        // Check if cancelled every 10 iterations
        if (i % 10 == 0 && cancellationToken.IsCancellationRequested)
        {
            // Return default or last best move
            return GetBestMoveFromPartialResults(moveResults);
        }
        
        foreach (var move in validMoves)
        {
            var sim = new SimulatedGameState(gameState);
            GameSimulator.ApplyMove(sim, move);
            var won = GameSimulator.SimulatePlayout(sim, sim.TrumpSuit, botTeam);
            
            float result = won ? 1.0f : 0.0f;
            moveResults[move].Add(result);
        }
    }
    
    return SelectBestMoveWithSoftPenalties(moveResults, gameState);
}
```

---

### Step 2: Update OnlineBot to Use Threading

**File:** `OnlineBot.cs`

**Current Code:**
```csharp
var decisionEngine = new GamePlay.BotV3.EuchreBotDecisionEngine(simulationCount);
var selectedCardData = await decisionEngine.SelectCardToPlay(botGameState);
```

**New Code:**
```csharp
var decisionEngine = new GamePlay.BotV3.EuchreBotDecisionEngine(simulationCount);

// Create cancellation token source for this decision
using var cts = CancellationTokenSource.CreateLinkedTokenSource(
    GamePlayControllerNetworked.CancellationTokenSource.Token);

// Set timeout (e.g., 10 seconds max)
cts.CancelAfter(TimeSpan.FromSeconds(10));

CardData selectedCardData;
try
{
    // Run on background thread
    selectedCardData = await UniTask.RunOnThreadPool(() => 
    {
        return decisionEngine.SelectCardToPlayThreaded(botGameState, cts.Token);
    }, cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    // Timeout or game ended - use fallback
    GameLogger.ShowLog("⚠️ Bot decision timeout, using quick fallback", GameLogger.LogType.Warning);
    selectedCardData = GetQuickFallbackMove(botGameState);
}
catch (Exception ex)
{
    // Unexpected error - use fallback
    GameLogger.ShowLog($"❌ Bot decision error: {ex.Message}", GameLogger.LogType.Error);
    selectedCardData = GetQuickFallbackMove(botGameState);
}
```

---

### Step 3: Add Fallback for Error Cases

**File:** `OnlineBot.cs`

**New Helper Method:**
```csharp
private CardData GetQuickFallbackMove(GamePlay.Bot.GameState gameState)
{
    // Simple heuristic when threading fails
    var validMoves = GamePlay.BotV3.GameSimulator.GetValidMoves(
        gameState.PlayerHand, 
        gameState.CurrentTrickSuit, 
        gameState.TrumpSuit);
    
    if (validMoves.Count == 1)
        return validMoves[0];
    
    // Play highest card if trying to win, else lowest
    if (gameState.CurrentTrickCards.Count > 0)
    {
        // Simplified logic: play lowest card as safe fallback
        return validMoves.OrderBy(c => c.GetCardPower(gameState.TrumpSuit, gameState.CurrentTrickSuit)).First();
    }
    
    // Lead with lowest card as safe fallback
    return validMoves.OrderBy(c => c.rank).First();
}
```

---

### Step 4: Add Optional UI Indicator

**File:** `OnlineBot.cs`

**Enhanced Implementation:**
```csharp
public override async UniTask<Card> PlayTurn(float time = 10f)
{
    if (IsDisabled) return null;
    RevealHand(handIsFaceUp);
    
    var botGameState = BuildBotGameState();
    var decisionEngine = new GamePlay.BotV3.EuchreBotDecisionEngine(simulationCount);
    
    // Show thinking indicator (optional)
    ShowThinkingIndicator(true);
    
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        GamePlayControllerNetworked.CancellationTokenSource.Token);
    cts.CancelAfter(TimeSpan.FromSeconds(10));
    
    CardData selectedCardData;
    try
    {
        selectedCardData = await UniTask.RunOnThreadPool(() => 
        {
            return decisionEngine.SelectCardToPlayThreaded(botGameState, cts.Token);
        }, cancellationToken: cts.Token);
    }
    catch (OperationCanceledException)
    {
        selectedCardData = GetQuickFallbackMove(botGameState);
    }
    catch (Exception ex)
    {
        GameLogger.ShowLog($"❌ Bot error: {ex.Message}", GameLogger.LogType.Error);
        selectedCardData = GetQuickFallbackMove(botGameState);
    }
    finally
    {
        // Hide thinking indicator
        ShowThinkingIndicator(false);
    }
    
    var card = hand.FirstOrDefault(c => c.cardData == selectedCardData);
    
    // Rest of method unchanged...
    await UniTask.Delay(Random.Range(1200, 2400), 
        cancellationToken: GamePlayControllerNetworked.CancellationTokenSource.Token);
    
    hand.Remove(card);
    GamePlayControllerNetworked.Instance.currentTrickCards[PlayerIndex] = card;
    
    var cardDataDto = JsonConvert.SerializeObject(new CardDataDto()
    {
        rank = card.cardData.rank,
        suit = card.cardData.suit
    });
    
    RPC_PlayCard(PlayerIndex, cardDataDto);
    await AnimateCardPlay(card);
    
    return card;
}

private void ShowThinkingIndicator(bool show)
{
    // Optional: Update UI to show bot is thinking
    // Could be a thought bubble, dots animation, etc.
    // Must be called on main thread
}
```

---

## 🧪 TESTING PROTOCOL

### Phase 1: Isolated Thread Test (30 minutes)
```
□ Create simple test scene
□ Create GameState with known values
□ Call SelectCardToPlayThreaded from button
□ Verify it returns correct result
□ Verify no errors in console
□ Verify Unity stays responsive during execution
□ Test with 10, 50, 100, 200 simulations
```

### Phase 2: Integration Test (1 hour)
```
□ Test in actual game vs bot
□ Verify bot makes reasonable decisions
□ Check console for any thread errors
□ Monitor frame rate (should be stable)
□ Test multiple consecutive bot turns
□ Test rapid game scenarios
```

### Phase 3: Network Test (1 hour)
```
□ Test multiplayer game with bots
□ Play for 15+ minutes continuously
□ Verify Photon connection stays stable
□ Check for any timeout warnings
□ Test bot playing during high network latency
□ Test with 2 bots in same game
```

### Phase 4: Stress Test (1 hour)
```
□ Increase simulations to 200
□ Play multiple games back-to-back
□ Monitor memory usage (check for leaks)
□ Test game cleanup during bot thinking
□ Test minimize/restore during bot turn
□ Test on different hardware specs
```

### Phase 5: Edge Cases (30 minutes)
```
□ Bot thinking when player disconnects
□ Bot thinking when game ends abruptly
□ Bot thinking when app loses focus
□ Bot thinking when device goes to sleep
□ Rapid-fire bot decisions (bot vs bot)
□ Cancel mid-calculation
```

---

## 📈 PERFORMANCE EXPECTATIONS

### With 100 Simulations (Current Level)
```
Background Thread Time: ~2 seconds
Main Thread Impact:     0ms (non-blocking)
Bot Intelligence:       Equal to current
Photon Timeout Risk:    0%
```

### With 150 Simulations (Recommended)
```
Background Thread Time: ~3 seconds
Main Thread Impact:     0ms (non-blocking)
Bot Intelligence:       50% smarter
Photon Timeout Risk:    0%
User Experience:        Improved (bot makes fewer mistakes)
```

### With 200 Simulations (Elite Mode)
```
Background Thread Time: ~4 seconds
Main Thread Impact:     0ms (non-blocking)
Bot Intelligence:       2x smarter
Photon Timeout Risk:    0%
User Experience:        Near-perfect bot play
```

---

## ⚠️ KNOWN LIMITATIONS & MITIGATIONS

### Limitation 1: First-Time Overhead
**Issue:** First thread spawn has ~50-100ms overhead  
**Impact:** First bot decision slightly slower  
**Mitigation:** Pre-warm thread pool at game start  
**Code:**
```csharp
// In game initialization:
await UniTask.RunOnThreadPool(() => { /* warm up */ });
```

---

### Limitation 2: Memory Overhead
**Issue:** Each SimulatedGameState allocates memory  
**Impact:** Increased GC pressure  
**Mitigation:** Object pooling  
**Code:**
```csharp
private static ObjectPool<SimulatedGameState> _statePool;

// Get from pool instead of new
var sim = _statePool.Get();
// ... use it ...
_statePool.Return(sim);
```

---

### Limitation 3: Can't Debug Easily
**Issue:** Background thread harder to debug in Unity  
**Impact:** Bugs harder to find  
**Mitigation:**  
1. Keep synchronous version for debugging  
2. Add extensive logging (collect on thread, display on main)  
3. Use conditional compilation

```csharp
#if UNITY_EDITOR && DEBUG_BOT_THREADING
    // Use synchronous version for debugging
    selectedCardData = decisionEngine.SelectCardToPlay(botGameState);
#else
    // Use threaded version for performance
    selectedCardData = await UniTask.RunOnThreadPool(...);
#endif
```

---

### Limitation 4: Platform Differences
**Issue:** Threading behavior varies by platform  
**Impact:** Might work on PC, fail on mobile  
**Mitigation:** Test on all target platforms  
**Platforms to Test:**
- Windows PC
- macOS
- iOS (if targeting)
- Android (if targeting)
- WebGL (threading limited - may need fallback)

---

## 🔄 GRADUAL ROLLOUT STRATEGY

### Week 1: Implementation
```
Day 1-2: Pre-implementation audit
Day 3-4: Core threading code
Day 5:   Isolated testing
```

### Week 2: Testing & Tuning
```
Day 1: Integration testing
Day 2: Network testing
Day 3: Stress testing
Day 4: Edge case testing
Day 5: Performance profiling
```

### Week 3: Optimization
```
Day 1-2: Implement object pooling
Day 3:   Add UI indicators
Day 4:   Cross-platform testing
Day 5:   Documentation & cleanup
```

### Week 4: Production Deployment
```
Day 1: Create feature branch
Day 2: Merge to dev branch
Day 3: Beta testing with real users
Day 4: Monitor metrics
Day 5: Production release or iterate
```

---

## 📋 SUCCESS METRICS

### Primary Goals
```
✓ Photon connection stable for 30+ minute games
✓ Bot intelligence equal or better than current
✓ No threading-related crashes
✓ Frame rate stays at 60fps during bot decisions
```

### Secondary Goals
```
✓ Bot thinking time < 5 seconds
✓ Memory usage increase < 50MB
✓ No visible UI freezes
✓ Works on all target platforms
```

### Stretch Goals
```
✓ Increase simulations to 200+
✓ Add dynamic difficulty scaling
✓ Add visual "bot thinking" indicators
✓ Profile and optimize to 1-2 second decisions
```

---

## 🚨 ROLLBACK TRIGGERS

Stop and revert if you encounter:

```
❌ Consistent crashes (>5% of games)
❌ Photon timeouts return (defeats purpose)
❌ Bot makes obviously bad decisions (intelligence regression)
❌ Frame rate drops below 30fps
❌ Memory leaks (>100MB growth per game)
❌ Platform-specific failures that can't be fixed quickly
❌ Development time exceeds 2 weeks
```

---

## 📚 REFERENCE IMPLEMENTATION

### Similar Games Using Threading
1. **Chess engines:** Stockfish, Leela Chess Zero
2. **Go engines:** AlphaGo, KataGo
3. **Card games:** Hearthstone AI, Magic: Arena AI
4. **Strategy games:** Civilization VI, XCOM

### Unity Threading Resources
- UniTask GitHub: https://github.com/Cysharp/UniTask
- Unity Job System: https://docs.unity3d.com/Manual/JobSystem.html
- C# Threading Best Practices: https://docs.microsoft.com/en-us/dotnet/standard/threading/

---

## ✅ PRE-IMPLEMENTATION CHECKLIST

Before starting threading implementation:

```
□ Quick fix deployed and tested (validates diagnosis)
□ Game stable for 15+ minutes with quick fix
□ CardData verified thread-safe
□ Random usage audited and ThreadSafeRandom created
□ All logging wrapped in conditional compilation
□ Backup/branch created
□ Team informed of implementation timeline
□ Testing plan approved
□ Rollback plan understood
□ Success metrics defined
```

---

## 🎯 FINAL RECOMMENDATION

**Timeline:**
- Implement quick fix today (30 min + 1 hour testing)
- If successful, schedule threading for next sprint (1 week)
- Conservative estimate: 2-3 weeks from decision to production

**Risk Level:** Medium (manageable with proper testing)

**Benefit:** High (eliminates timeout + allows smarter bot)

**Priority:** High for scaling, Medium for immediate release

**Go/No-Go Decision:** Test quick fix first, then decide based on results

---

**Document Version:** 1.0  
**Last Updated:** December 1, 2025  
**Next Review:** After quick fix validation  
**Owner:** Development Team
