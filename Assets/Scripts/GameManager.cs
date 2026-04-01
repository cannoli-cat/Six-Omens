using System.Collections;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour {
    [SerializeField] private int cardAmount = 6;
    [SerializeField] private int basePayout = 100;
    [SerializeField] private float streakStep = 0.35f;
    [SerializeField] private float streakExtraCap = 4f;
    [SerializeField] private float speedMaxBonus = 0.50f;
    [SerializeField] private float maxMultCap = 8f;
    [SerializeField] private int flipTimeCost = 4;
    [SerializeField] private int peekTimeCost = 1;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private int offerEveryNClients = 4;
    [SerializeField] private int startingBalance = 200;

    [Header("Refs")] [SerializeField] private UIController ui;
    [SerializeField] private Clock clock;
    [SerializeField] private Sprite deathCard;
    [SerializeField] private Sprite haloCard;

    public static GameManager Instance { get; private set; }
    public int Balance { get; private set; }

    private OmenCardData[] currentCards;
    private int clientsProcessed, numDied, numLived;
    private int streak = 0;
    private int roundStartSeconds = 30;
    private Item[] slotItems = new Item[4];
    private bool offerOpen;
    private bool roundResolved;
    private bool gameOver;
    private bool wasTimerRunning;

    private enum GuessType {
        None,
        WillDie,
        WillLive
    }

    private GuessType guess = GuessType.None;

    private bool phoneAnswered;
    private bool guessMade;
    private bool timedOut;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!ui) ui = FindFirstObjectByType<UIController>();
        if (!clock) clock = FindFirstObjectByType<Clock>();

        ui.onCutsceneComplete += StartRun;
        ui.onPhoneAnswered += OnPhoneAnswered;

        ui.onGuessWillDie += () => OnGuess(GuessType.WillDie);
        ui.onGuessWillLive += () => OnGuess(GuessType.WillLive);

        ui.onFlipOne += FlipOne;
        ui.onPeekRandom += PeekOne;

        ui.onCashOut += OnCashout;

        ui.onUseItemSlot += UseItemAtSlot;

        clock.OnTimeUp += OnTimeUp;

        Time.timeScale = 1f;
    }

    private void StartRun() {
        gameOver = false;
        
        MusicPlayer.Instance?.PlayGameMusic();
        clock.ResetTimer();
        Balance = startingBalance;
        clientsProcessed = 0;
        numDied = 0;
        numLived = 0;
        streak = 0;
        slotItems = new Item[4];
        offerOpen = false;

        ui.UpdateBalance(Balance);
        ui.UpdateProcessed(clientsProcessed);
        ui.UpdateMultiplier(1f);
        ui.UpdateHighScore(PlayerPrefs.GetInt("HighScore", 0));

        StartCoroutine(RunLoop());
    }
    
    private IEnumerator RunLoop() {
        while (!gameOver) {
            if (Balance > startingBalance) ui.SetCashoutInteractive(true);

            clock.PauseTimer();
            StartPhonePhase();
            yield return new WaitUntil(() => phoneAnswered || gameOver);
            if (gameOver) yield break; // stop the loop if game ended

            if (ShouldOfferNow()) yield return OfferItemsFlow();

            currentCards = GenerateOmenCards(cardAmount);
            EnsureNotEven(currentCards);
            clock.ResetTimer();

            ui.SetDecisionControlsEnabled(false);
            yield return ui.Next(currentCards);

            ResetDecisionFlags();
            roundStartSeconds = clock.RemainingSeconds;

            ui.SetDecisionControlsEnabled(true);
            clock.StartTimer();

            yield return new WaitUntil(() => guessMade || timedOut || gameOver);
            if (gameOver) yield break;

            clock.PauseTimer();
            ResolveRound();
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }

    private void StartPhonePhase() {
        if (gameOver) return;
        phoneAnswered = false;
        timedOut = false;
        guessMade = false;
        roundResolved = false;          
        guess = GuessType.None;
        ui.StartPhoneRing();
    }

    private void ResetDecisionFlags() {
        guessMade = false;
        timedOut = false;
        guess = GuessType.None;
    }

    private void ResolveRound() {
        var delta = 0;
        var correct = false;

        if (!timedOut && guessMade) {
            var skulls = currentCards.Count(c => c.isSkull);
            var halos  = cardAmount - skulls;
            switch (guess) {
                case GuessType.WillDie:  correct = skulls > halos; if (correct) numDied++; break;
                case GuessType.WillLive: correct = halos  > skulls; if (correct) numLived++; break;
            }
        }

        var mActive = ComputeMultiplier();

        if (correct) {
            streak++;
            delta += Mathf.RoundToInt(basePayout * mActive);

            ui.UpdateMultiplier(mActive);                
            StartCoroutine(RightGuessSpeedPop());        
            ui.PlayRightGuessFX();          
        } else {
            delta -= Mathf.RoundToInt(basePayout * mActive);
            streak = 0;
            ui.UpdateMultiplier(1f);
            StartCoroutine(WrongGuessHitStop());
            ui.PlayWrongGuessFX();
        }

        Balance += delta;
        clientsProcessed++;
        if (Balance < 0) { LoseGame(); return; }
        ui.UpdateBalance(Balance);
        ui.UpdateProcessed(clientsProcessed);
        ui.StartPhoneRing();
    }
    
    private void OnPhoneAnswered() {
        ui.StopPhoneRing();
        phoneAnswered = true;
    }

    private void OnGuess(GuessType choice) {
        if (guessMade || roundResolved) return;
        guess = choice;
        guessMade = true;
        roundResolved = true;                    

        clock.PauseTimer();
        clock.ResetTimer();

        ui.SetDecisionControlsEnabled(false);
        ui.SetCardsInteractive(false);
        StartCoroutine(ui.ClearCards());
    }

    private void OnTimeUp() {
        if (timedOut || roundResolved) return;
        timedOut = true;
        roundResolved = true;                    

        clock.PauseTimer();
        clock.ResetTimer();

        ui.SetDecisionControlsEnabled(false);
        ui.SetCardsInteractive(false);
        StartCoroutine(ui.ClearCards());
    }

    private OmenCardData[] GenerateOmenCards(int amount) {
        var cards = new OmenCardData[amount];
        for (var i = 0; i < amount; i++) cards[i] = GenerateOmenCard();
        return cards;
    }

    private OmenCardData GenerateOmenCard() {
        var isSkull = Random.value < 0.5f;
        var icon = isSkull ? deathCard : haloCard;
        return new OmenCardData(isSkull, icon);
    }

    public void Pause() {
        Time.timeScale = 0;
        wasTimerRunning = clock.IsRunning;  
        clock.PauseTimer();
    }

    public void UnPause() {
        Time.timeScale = 1;
        if (wasTimerRunning) {
            clock.ResumeTimer();
        }
    }

    public void PauseTimerForSeconds(float seconds) {
        clock.FreezeForSeconds(seconds);
    }

    public void Reveal(int cards) {
        ui.Reveal(cards);
    }

    private void OnCashout() {
        if (Balance > PlayerPrefs.GetInt("HighScore", 0)) {
            PlayerPrefs.SetInt("HighScore", Balance);
            PlayerPrefs.Save();
        }
        gameOver = true;
        clock.PauseTimer();
        ui.StopPhoneRing();
        SFXPlayer.Instance?.StopPhoneRing();
        ui.WinGame(Balance, PlayerPrefs.GetInt("HighScore"), numDied, numLived, clientsProcessed);
    }

    private void LoseGame() {
        gameOver = true;
        clock.PauseTimer();
        ui.StopPhoneRing();
        SFXPlayer.Instance?.StopPhoneRing();
        ui.LoseGame(Balance, PlayerPrefs.GetInt("HighScore"), numDied, numLived, clientsProcessed);
    }

    private float ComputeMultiplier() {
        var mStreak = 1f + Mathf.Min(streak * streakStep, streakExtraCap);
        var start = Mathf.Max(1, roundStartSeconds);
        var mSpeed = 1f + speedMaxBonus * (clock.RemainingSeconds / start);

        return Mathf.Clamp(mStreak * mSpeed, 1f, maxMultCap);
    }

    private void FlipOne() {
        if (!clock.IsRunning || clock.RemainingSeconds <= 0) return;

        ui.Reveal(1);
        
        if (!ui.HasUnrevealed()) {
            AutoResolveFromBoard();
            return;
        }
        
        clock.AddSeconds(-flipTimeCost);
        if (clock.RemainingSeconds <= 0f) {
            ForceTimeoutNow();
        }
    }

    private void PeekOne() {
        if (!clock.IsRunning || clock.RemainingSeconds <= 0) return;

        ui.PeekRandom(1f);

        clock.AddSeconds(-peekTimeCost);

        if (clock.RemainingSeconds <= 0f) {
            ForceTimeoutNow();
        }
    }


    private int GetFirstFreeSlotIndex() {
        for (var i = 0; i < slotItems.Length; i++)
            if (slotItems[i] == null)
                return i;
        return -1;
    }

    private IEnumerator OfferItemsFlow() {
        var free = CountFreeSlots();
        var dbCount = itemDatabase ? (itemDatabase.items?.Length ?? 0) : 0;

        if (free <= 0 || !itemDatabase || dbCount == 0 || offerOpen) {
            Debug.Log(
                $"Offer: blocked (free={free}, db={(itemDatabase ? "ok" : "null")}, dbCount={dbCount}, open={offerOpen})");
            yield break;
        }

        offerOpen = true;

        var maxGive = Mathf.Min(4, free, dbCount);
        var target = Random.Range(1, maxGive + 1);

        Debug.Log($"Offer: free={free} dbCount={dbCount} maxGive={maxGive} target={target}");

        var added = 0;
        var guard = 0;
        const int maxAttempts = 50;

        while (added < target && guard++ < maxAttempts) {
            var slot = GetFirstFreeSlotIndex();
            if (slot < 0) break;

            if (itemDatabase.items != null) {
                var bp = itemDatabase.items[Random.Range(0, dbCount)];
                var runtime = ItemFactory.Build(bp);
                if (runtime == null || runtime.icon == null) continue;

                var placed = ui.AddInventoryIconAt(slot, runtime.icon, runtime.Description);
                if (!placed) {
                    Debug.LogWarning($"Offer: UI refused slot {slot} (missing button?).");
                    break;
                }

                slotItems[slot] = runtime;
                added++;
                Debug.Log($"Offer: +'{runtime.Description}' at slot {slot} (added={added}/{target})");
            }
        }

        if (added == 0) {
            Debug.LogWarning("Offer: added 0 items after attempts; fallback sweep.");
            for (var i = 0; i < dbCount; i++) {
                var r = ItemFactory.Build(itemDatabase.items[i]);
                if (r == null || r.icon == null) continue;

                var slot = GetFirstFreeSlotIndex();
                if (slot < 0) break;

                if (ui.AddInventoryIconAt(slot, r.icon, r.Description)) {
                    slotItems[slot] = r;
                    added = 1;
                    Debug.Log($"Offer: fallback added '{r.Description}' into slot {slot}");
                    break;
                }
            }
        }

        Debug.Log($"Offer: finished, added={added}");
        offerOpen = false;
    }

    private bool ShouldOfferNow() {
        return HasFreeSlot() && (clientsProcessed == 0 || (clientsProcessed % offerEveryNClients) == 0);
    }

    private void UseItemAtSlot(int slot) {
        if (slot < 0 || slot >= slotItems.Length) return;
        var item = slotItems[slot];
        if (item == null) return;

        item.Use();

        if (item.kind == ItemKind.Consumable) {
            slotItems[slot] = null;
            ui.RemoveInventoryIcon(slot);
        }
    }

    private int CountFreeSlots() => slotItems.Count(slotItem => slotItem == null);
    private bool HasFreeSlot() => CountFreeSlots() > 0;

    private void EnsureNotEven(OmenCardData[] cards) {
        var skulls = cards.Count(c => c.isSkull);
        var halos = cards.Length - skulls;

        if (skulls == halos) {
            var idx = Random.Range(0, cards.Length);
            var newIsSkull = !cards[idx].isSkull;
            cards[idx] = new OmenCardData(newIsSkull, newIsSkull ? deathCard : haloCard);
        }
    }
    
    private IEnumerator WrongGuessHitStop() {
        var old = Time.timeScale;
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.12f);
        Time.timeScale = old;
    }
    
    private IEnumerator RightGuessSpeedPop() {
        var old = Time.timeScale;
        Time.timeScale = 1.15f;
        yield return new WaitForSecondsRealtime(0.12f);
        Time.timeScale = old;
    }
    
    public string GetItemDescriptionAtSlot(int slot) {
        if (slot < 0 || slot >= slotItems.Length) return null;
        var it = slotItems[slot];
        return it?.Description;
    }
    
    public void AddTime(int seconds) {
        clock.AddSeconds(seconds);
    }
    
    public void FreePeek(int charges) {
        if (charges <= 0) return;
        ui.StartTargetPeek(charges); 
    }

    public void FreeFlip(int charges) {
        if (charges <= 0) return;
        ui.StartTargetFlip(charges);
    }
    
    private void ForceTimeoutNow() {
        if (roundResolved) return;
        timedOut = true;
        roundResolved = true;

        clock.PauseTimer();
        clock.ResetTimer();

        ui.SetDecisionControlsEnabled(false);
        ui.SetCardsInteractive(false);
        StartCoroutine(ui.ClearCards());
    }
    
    private void AutoResolveFromBoard() {
        if (roundResolved) return;

        var skulls = currentCards.Count(c => c.isSkull);
        var halos  = cardAmount - skulls;
        if (skulls == halos) return;

        guess = (skulls > halos) ? GuessType.WillDie : GuessType.WillLive;
        guessMade = true;
        roundResolved = true;

        clock.PauseTimer();
        clock.ResetTimer();

        ui.SetDecisionControlsEnabled(false);
        ui.SetCardsInteractive(false);
        StartCoroutine(ui.ClearCards());
    }
    
    public void NotifyAllRevealed() {
        AutoResolveFromBoard();
    }
}