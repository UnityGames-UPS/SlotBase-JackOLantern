using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] internal UIManager uiManager;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private SlotView slotView;

    [Header("Win Settings")]
    [SerializeField] private double bigWinMultiplierThreshold = 500.0;

    internal GameConfig gameConfig;
    internal PlayerData playerData;
    internal SpinResult lastResult;

    internal GameState currentState;
    internal SpinSpeed currentSpinSpeed;

    internal int currentBetIndex;
    internal double currentBetAmount;

    internal bool isAutoPlaying;
    internal int autoPlayTotalRounds;
    internal int autoPlayRemainingRounds;
    internal bool wasAutoPlayingBeforeFreeSpins;
    internal int savedAutoPlayRemainingRounds;
    internal int savedAutoPlayTotalRounds;

    internal bool isInFreeSpins;
    internal int freeSpinsRemaining;
    internal int freeSpinsUsed;
    internal bool waitingForFreeSpinStart;
    internal double CurrentFreeSpinWin => totalFreeSpinWin;
    internal ServerFreeGamesState CurrentFreeGamesState { get; private set; }
    private bool isFreeSpinPresentationPending;
    internal bool AreFreeSpinControlsLocked =>
        isInFreeSpins ||
        waitingForFreeSpinStart ||
        isFreeSpinPresentationPending ||
        lastResult?.freeSpinData?.isTriggered == true ||
        (lastResult?.freeGames?.active == true && lastResult.serverSpinsRemaining > 0);

    private int totalFreeSpinsAwarded;
    private double totalFreeSpinWin;

    internal bool isInitialized;
    internal bool initializationFailed;

    private Coroutine spinCoroutine;
    private bool stopRequested;
    private bool waitingForSpecialWin;

    #region Initialization

    private void Start()
    {
        currentState = GameState.Initializing;
        currentSpinSpeed = SpinSpeed.Normal;
        waitingForFreeSpinStart = false;
        isInitialized = false;
        initializationFailed = false;
    }

    internal void OnInitDataReceived(GameConfig config, PlayerData player)
    {
        gameConfig = config;
        playerData = player;
        currentBetIndex = playerData.currentBetIndex;
        UpdateBetAmount();

        isInitialized = true;
        currentState = GameState.Idle;

        uiManager.OnGameInitialized();
    }

    #endregion

    #region Bet Management

    internal void IncreaseBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying || AreFreeSpinControlsLocked) return;
        if (gameConfig == null || gameConfig.availableBets == null || gameConfig.availableBets.Count == 0) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex + 1;
        if (nextIndex > maxIndex)
        {
            nextIndex = 0;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void DecreaseBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying || AreFreeSpinControlsLocked) return;
        if (gameConfig == null || gameConfig.availableBets == null || gameConfig.availableBets.Count == 0) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex - 1;
        if (nextIndex < 0)
        {
            nextIndex = maxIndex;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void SetBetIndex(int index)
    {
        if (AreFreeSpinControlsLocked) return;

        currentBetIndex = index;
        UpdateBetAmount();
        uiManager.UpdateBetDisplay();
        if (slotView != null) slotView.OnBetChanged();
    }

    private void UpdateBetAmount()
    {
        currentBetAmount = gameConfig.availableBets[currentBetIndex];
    }

    #endregion

    #region Spin Control
    
    internal void RequestSpin()
    {
        if (waitingForFreeSpinStart) return;
        if (AreFreeSpinControlsLocked && !isInFreeSpins) return;

        if (currentState != GameState.Idle) return;
        if (!socketManager.isConnected) return;

        double totalPay = GetTotalPay();
        if (!isInFreeSpins && playerData.balance < totalPay)
        {
            if (popupManager != null)
            {
                popupManager.ShowInsufficientFundsError();
            }
            return;
        }

        StartSpin();
    }

    internal void RequestStop()
    {
        if (currentState == GameState.Spinning || currentState == GameState.Stopping)
        {
            if (isAutoPlaying)
            {
                StopAutoPlay();
            }
            else if (!isInFreeSpins)
            {
                stopRequested = true;
                slotView?.RequestManualStop();
                uiManager.DisableSpinButtonDuringStop();
            }
        }
    }

    private void StartSpin()
    {
        if (lastResult != null)
        {
            ProcessSpinResult();
        }

        lastResult = null;
        currentState = GameState.Spinning;
        stopRequested = false;

        // Deduct total pay from balance on spin start (except in free spins)
        if (!isInFreeSpins)
        {
            playerData.balance -= GetTotalPay();
            if (playerData.balance < 0) playerData.balance = 0;
        }

        uiManager.OnSpinStarted();

        if (slotView != null)
        {
            slotView.StartSpin();
        }

        socketManager.SendSpinRequest(currentBetIndex, isInFreeSpins);

        if (spinCoroutine != null)
            StopCoroutine(spinCoroutine);
        spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        while (lastResult == null)
        {
            yield return null;
        }

        currentState = GameState.Stopping;

        if (slotView != null && lastResult.resultMatrix != null)
        {
            if (stopRequested)
            {
                slotView.StopSpinTogether(lastResult.resultMatrix, OnReelsStoppedComplete);
            }
            else if (currentSpinSpeed == SpinSpeed.QuickSpin)
            {
                slotView.QuickStop(lastResult.resultMatrix, OnReelsStoppedComplete);
            }
            else
            {
                slotView.StopSpin(lastResult.resultMatrix, OnReelsStoppedComplete);
            }
        }
        else
        {
            OnReelsStoppedComplete();
        }
    }

    private void OnReelsStoppedComplete()
    {
        if (lastResult != null)
        {
            playerData = new PlayerData
            {
                balance = lastResult.playerData != null ? lastResult.playerData.balance : playerData.balance,
                currentBetIndex = lastResult.playerData != null ? lastResult.playerData.currentBetIndex : currentBetIndex
            };
        }

        if (lastResult != null && lastResult.winAmount > 0 && lastResult.winLines != null && lastResult.winLines.Count > 0)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;
            bool isFreeSpinTrigger = lastResult.freeSpinData != null && lastResult.freeSpinData.isTriggered;

            // A mid-spin BigWin pauses normal play, autoplay, and individual
            // free games. Free-game trigger/End presentation remains separate.
            if (multiplier >= bigWinMultiplierThreshold && !isFreeSpinTrigger)
            {
                uiManager.DisableControlsDuringWinAnimation();
                currentState = GameState.Idle;
                slotView.ShowWinLineAnimation(lastResult.winLines, OnWinAnimationComplete);
                StartCoroutine(TriggerWinPopupWithDelay(1.5f, lastResult));
            }
            else
            {
                // For normal wins, trigger UI update immediately and enable controls
                uiManager.OnSpinStopping(lastResult);
                uiManager.EnableControlsAfterWinAnimation();
                uiManager.OnSpinCompleted(lastResult);
                currentState = GameState.Idle;
                if (!isFreeSpinTrigger)
                {
                    AudioManager.Instance?.PlayNormalWin();
                }
                slotView.ShowWinLineAnimation(lastResult.winLines, OnWinAnimationComplete);
            }
        }
        else
        {
            uiManager.OnSpinStopping(lastResult);
            currentState = GameState.Idle;
            OnWinAnimationComplete();
        }
    }

    private IEnumerator TriggerWinPopupWithDelay(float delay, SpinResult result)
    {
        double totalPay = GetTotalPay();
        double multiplier = totalPay > 0 ? (result.winAmount / totalPay) : 0;
        if (multiplier < bigWinMultiplierThreshold)
        {
            waitingForSpecialWin = false;
            yield break;
        }

        waitingForSpecialWin = true;

        yield return new WaitForSeconds(delay);

        if (lastResult == result && multiplier >= bigWinMultiplierThreshold)
        {
            uiManager.TriggerBigWinPopup(result, () =>
            {
                waitingForSpecialWin = false;
            });
        }
        else
        {
            waitingForSpecialWin = false;
        }
    }

    private void OnWinAnimationComplete()
    {
        if (lastResult != null)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;
            bool isFreeSpinTrigger = lastResult.freeSpinData != null && lastResult.freeSpinData.isTriggered;

            // Only update UI here if it wasn't already updated in OnReelsStoppedComplete (multiplier < bigWinMultiplierThreshold)
            if (multiplier >= bigWinMultiplierThreshold && !isFreeSpinTrigger)
            {
                uiManager.OnSpinStopping(lastResult);
            }
        }

        StartCoroutine(ProcessSpecialFeaturesAfterWin());
    }

    private IEnumerator ProcessSpecialFeaturesAfterWin()
    {
        // Wait for special win popup to finish before starting special features
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        if (lastResult != null && lastResult.freeSpinData != null && lastResult.freeSpinData.isTriggered)
        {
            yield return StartCoroutine(DelayScatterTriggerResult());
            yield break;
        }

        ResumeAfterSpecialFeature();
    }

    private void ResumeAfterSpecialFeature()
    {
        if (isAutoPlaying || isInFreeSpins)
        {
            StartCoroutine(DelayBeforeNextRound());
        }
        else
        {
            ProcessSpinResult();
        }
    }

    private IEnumerator DelayScatterTriggerResult()
    {
        if (slotView != null)
        {
            slotView.AnimateAllScatters(2);
        }

        yield return new WaitForSeconds(2f);
        ProcessSpinResult();
    }

    private IEnumerator DelayBeforeNextRound()
    {
        float delayTime = currentSpinSpeed == SpinSpeed.QuickSpin ? 0.3f : 0.5f;
        yield return new WaitForSeconds(delayTime);

        // Wait for special win popup using the flag and active state
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        ProcessSpinResult();
    }

    internal void OnSpinResultReceived(SpinResult result)
    {
        lastResult = result;
        CurrentFreeGamesState = result?.freeGames;

        if (result?.freeSpinData != null && result.freeSpinData.isTriggered)
        {
            uiManager?.DisableSpinButtonInteraction();
        }

        if (isInFreeSpins)
        {
            ApplyFreeGamesState(result);
        }
    }

    private void ApplyFreeGamesState(SpinResult result)
    {
        ServerFreeGamesState state = result.freeGames;

        if (state != null)
        {
            int previousTotal = totalFreeSpinsAwarded;
            totalFreeSpinsAwarded = Mathf.Max(0, state.totalSpins);

            if (state.retriggered && result.freeSpinData != null)
            {
                result.freeSpinData.spinsAwarded =
                    Mathf.Max(0, totalFreeSpinsAwarded - previousTotal);
            }

            totalFreeSpinWin = System.Math.Max(0, state.featureWin ?? state.totalWinCash
                ?? (totalFreeSpinWin + result.winAmount));
        }
        else
        {
            if (result.freeSpinData?.isTriggered == true)
            {
                totalFreeSpinsAwarded += Mathf.Max(0, result.freeSpinData.spinsAwarded);
            }

            totalFreeSpinWin += result.winAmount;
        }

        freeSpinsRemaining = result.serverSpinsRemaining;
        freeSpinsUsed = Mathf.Max(0, totalFreeSpinsAwarded - freeSpinsRemaining);

        bool isRetrigger = result.freeSpinData?.isTriggered == true
            && result.freeSpinData.spinsAwarded > 0;

        // Keep the old counter visible during an extra-games announcement.
        if (!isRetrigger)
        {
            uiManager.UpdateFreeSpinCount(freeSpinsUsed, totalFreeSpinsAwarded);
        }
    }

    private void ProcessSpinResult()
    {
        playerData = lastResult.playerData;

        uiManager.OnSpinCompleted(lastResult);

        bool isFreeSpinRetrigger =
            isInFreeSpins &&
            lastResult.freeSpinData != null &&
            lastResult.freeSpinData.isTriggered &&
            lastResult.freeSpinData.spinsAwarded > 0;
        int retriggeredSpins = isFreeSpinRetrigger
            ? lastResult.freeSpinData.spinsAwarded
            : 0;

        int serverSpinsRemaining = lastResult.serverSpinsRemaining;
        if (isInFreeSpins && freeSpinsRemaining != serverSpinsRemaining)
        {
            freeSpinsRemaining = serverSpinsRemaining;
        }

        bool hasActiveServerSession =
            lastResult.freeGames?.active == true &&
            !lastResult.freeGames.isCompleted &&
            lastResult.serverSpinsRemaining > 0;
        bool hasFreeSpinTrigger = lastResult.freeSpinData?.isTriggered == true;

        if (!isInFreeSpins && (hasActiveServerSession || hasFreeSpinTrigger))
        {
            StartFreeSpins(lastResult.freeSpinData?.spinsAwarded ?? lastResult.serverSpinsRemaining);
            lastResult = null;
            return;
        }

        if (isFreeSpinRetrigger)
        {
            // The server has already included the newly awarded games in the
            // remaining count. Pause here so Start can be shown again without
            // resetting the accumulated feature win or session state.
            waitingForFreeSpinStart = true;
            currentState = GameState.Idle;
            lastResult = null;
            uiManager.OnFreeSpinsRetriggered(retriggeredSpins);
            return;
        }

        lastResult = null;

        if (isAutoPlaying && !isInFreeSpins)
        {
            if (autoPlayTotalRounds != -1)
            {
                autoPlayRemainingRounds--;
            }

            uiManager.UpdateAutoPlayCount();

            if (autoPlayTotalRounds != -1 && autoPlayRemainingRounds <= 0)
            {
                currentState = GameState.Idle;
                StopAutoPlay();
            }
            else
            {
                // Before requesting the next spin, verify the player can still afford it.
                // If not, stop autoplay (restores all UI) then show the popup.
                double totalPay = GetTotalPay();
                if (playerData.balance < totalPay)
                {
                    currentState = GameState.Idle;
                    StopAutoPlay();
                    if (popupManager != null) popupManager.ShowInsufficientFundsError();
                }
                else
                {
                    currentState = GameState.Idle;
                    RequestSpin();
                }
            }
        }
        else if (isInFreeSpins)
        {
            if (freeSpinsRemaining <= 0)
            {
                EndFreeSpins(totalFreeSpinWin, freeSpinsUsed);
            }
            else
            {
                currentState = GameState.Idle;
                StartCoroutine(DelayBeforeNextFreeSpin());
            }
        }
        else
        {
            currentState = GameState.Idle;
        }
    }

    #endregion

    #region Spin Speed Control

    internal void SetSpinSpeed(SpinSpeed speed)
    {
        currentSpinSpeed = speed;
        slotView?.OnSpinSpeedChanged(speed);
    }

    #endregion



    #region Auto Play

    internal void StartAutoPlay(int rounds)
    {
        if (currentState != GameState.Idle || AreFreeSpinControlsLocked) return;

        // Check balance BEFORE locking any UI — if insufficient, show popup and bail.
        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = rounds;
        autoPlayRemainingRounds = rounds;
        wasAutoPlayingBeforeFreeSpins = false;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    internal void StopAutoPlay()
    {
        isAutoPlaying = false;
        autoPlayRemainingRounds = 0;
        wasAutoPlayingBeforeFreeSpins = false;

        uiManager.OnAutoPlayStopped();
    }

    internal bool ShouldResumeAutoPlay()
    {
        return wasAutoPlayingBeforeFreeSpins && (savedAutoPlayTotalRounds == -1 || savedAutoPlayRemainingRounds > 0);
    }

    internal void ResumeAutoPlay()
    {
        if (AreFreeSpinControlsLocked || !ShouldResumeAutoPlay()) return;

        int remaining = savedAutoPlayRemainingRounds;
        int total = savedAutoPlayTotalRounds;
        wasAutoPlayingBeforeFreeSpins = false;

        if (currentState != GameState.Idle) return;

        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = total;
        autoPlayRemainingRounds = remaining;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    #endregion

    #region Free Spins

    private void StartFreeSpins(int spins)
    {
        isFreeSpinPresentationPending = true;
        ServerFreeGamesState state = CurrentFreeGamesState;
        bool resumeExisting = state?.active == true && !state.isCompleted;

        isInFreeSpins = true;
        freeSpinsRemaining = Mathf.Max(0, state?.spinsRemaining ?? spins);
        totalFreeSpinsAwarded = Mathf.Max(0, state?.totalSpins ?? spins);
        freeSpinsUsed = Mathf.Max(0, totalFreeSpinsAwarded - freeSpinsRemaining);
        totalFreeSpinWin = System.Math.Max(0, state?.featureWin ?? state?.totalWinCash ?? 0);
        waitingForFreeSpinStart = true;
        AudioManager.Instance?.PlayFreeSpinBg();

        int prevTotal = autoPlayTotalRounds;
        int prevRemaining = autoPlayRemainingRounds;

        if (isAutoPlaying)
        {
            StopAutoPlay();
            wasAutoPlayingBeforeFreeSpins = true;
            savedAutoPlayTotalRounds = prevTotal;
            savedAutoPlayRemainingRounds = (prevTotal != -1) ? (prevRemaining - 1) : -1;
        }

        double triggeringGameWin = System.Math.Max(0,
            CurrentFreeGamesState?.gameWin ?? lastResult?.winAmount ?? 0);
        uiManager.OnFreeSpinsStarted(freeSpinsRemaining, triggeringGameWin, resumeExisting);

        currentState = GameState.Idle;
    }

    internal void StartFirstFreeSpin()
    {
        waitingForFreeSpinStart = false;

        StartCoroutine(DelayBeforeFirstFreeSpin());
    }

    internal void ContinueFreeSpinsAfterRetrigger()
    {
        if (!isInFreeSpins || !waitingForFreeSpinStart) return;

        waitingForFreeSpinStart = false;
        currentState = GameState.Idle;
        StartCoroutine(DelayBeforeFirstFreeSpin());
    }


    private IEnumerator DelayBeforeFirstFreeSpin()
    {
        yield return new WaitForSeconds(0.5f);
        RequestSpin();
    }

    private IEnumerator DelayBeforeNextFreeSpin()
    {
        yield return new WaitForSeconds(0.3f);

        // Wait for special win popup if it's still active or pending
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        RequestSpin();
    }

    private void EndFreeSpins(double totalRoundWin, int totalSpinsUsed)
    {
        isFreeSpinPresentationPending = true;
        isInFreeSpins = false;
        freeSpinsRemaining = 0;
        totalFreeSpinsAwarded = 0;
        AudioManager.Instance?.PlayMainBg();

        uiManager.OnFreeSpinsEnded(totalRoundWin, totalSpinsUsed);

        currentState = GameState.Idle;
    }

    internal void CompleteFreeSpinPresentation()
    {
        isFreeSpinPresentationPending = false;
    }

    #endregion

    #region Connection Events

    internal void OnDisconnected()
    {
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }

        wasAutoPlayingBeforeFreeSpins = false;
        if (isAutoPlaying)
        {
            StopAutoPlay();
        }

        currentState = GameState.Idle;
        // Note: The disconnection popup is shown by SocketIOManager.OnSocketDisconnected()
        // to avoid duplicates. GameManager only cleans up state here.
    }

    internal void ExitGame()
    {
        socketManager.CloseSocket();

    }

    #endregion

    #region Helper Methods

    internal double GetTotalPay()
    {
        int activeLines = gameConfig != null && gameConfig.paylineCount > 0
            ? gameConfig.paylineCount
            : 40;
        return currentBetAmount * activeLines;
    }

    internal bool CanAffordBet()
    {
        double totalPay = GetTotalPay();
        return playerData.balance >= totalPay;
    }

    internal bool IsSpinning()
    {
        return currentState == GameState.Spinning || currentState == GameState.Stopping;
    }

    /// <summary>
    /// Returns true if at least one scatter symbol appears anywhere in the result matrix.
    /// Uses the server-configured scatterSymbolId (default 12) as the reference ID.
    /// </summary>
    private bool ResultMatrixHasScatter(List<List<int>> matrix)
    {
        if (matrix == null) return false;

        int scatterId = gameConfig != null ? gameConfig.scatterSymbolId : 1;

        foreach (var col in matrix)
        {
            if (col == null) continue;
            foreach (int sym in col)
            {
                if (sym == scatterId) return true;
            }
        }

        return false;
    }

    #endregion
}
