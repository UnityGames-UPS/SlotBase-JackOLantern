using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using DG.Tweening;
using Spine.Unity;

public class SlotView : MonoBehaviour
{
    private const int ScatterCountForAnticipation = 2;
    private const int VisibleRowCount = 4;
    private const float ReelStartY = 783f;
    private const float ReelEndY = -774f;
    private const float LandingStartOffset = 350f;
    private const float LandingDuration = 0.25f;
    private const float NormalStopSpacing = 0.2f;
    private const float ResultAssignmentProgress = 0.95f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Win Line Presentation")]
    [Tooltip("Line1 through Line40, in server lineIndex order (0 through 39).")]
    [SerializeField] private List<GameObject> winLineObjects = new List<GameObject>(40);
    [SerializeField] private TMPro.TMP_Text winAmountTopText;
    [SerializeField] private TMPro.TMP_Text winAmountMidText;
    [SerializeField] private TMPro.TMP_Text winAmountLowerMidText;
    [SerializeField] private TMPro.TMP_Text winAmountBottomText;

    [Header("Existing Winboxes - Five Reels, Four Rows Each")]
    [Tooltip("Winbox/Slot through Slot (4), with each reel's Images 1 through 4 in top-to-bottom order.")]
    [SerializeField] private List<ReelImages> winBoxImages = new List<ReelImages>(5);

    [Header("Symbol Sprites - Assign by Name")]
    [FormerlySerializedAs("spriteLantern"), SerializeField] private Sprite symbolWildSprite;       // ID: 0
    [FormerlySerializedAs("spriteHammer"), SerializeField] private Sprite symbolScatterSprite;    // ID: 1
    [SerializeField] private Sprite symbolMummySprite;       // ID: 2
    [SerializeField] private Sprite symbolGoblinSprite;      // ID: 3
    [SerializeField] private Sprite symbolVampireSprite;     // ID: 4
    [SerializeField] private Sprite symbolWerewolfSprite;    // ID: 5
    [SerializeField] private Sprite symbolASprite;           // ID: 6
    [SerializeField] private Sprite symbolKSprite;           // ID: 7
    [SerializeField] private Sprite symbolQSprite;           // ID: 8
    [SerializeField] private Sprite symbolJSprite;           // ID: 9
    [SerializeField] private Sprite symbolTenSprite;         // ID: 10
    [SerializeField] private Sprite symbolNineSprite;        // ID: 11

    // Internal array built from named sprites
    private Sprite[] symbolSprites;

    [Header("Reel Containers")]
    [SerializeField] private Transform[] reelTransforms;

    [Header("Spin Strip Images (fixed symbols; never changed while spinning)")]
    [SerializeField] private List<ReelImages> reelImagesList;

    [System.Serializable]
    private sealed class ResultReelImages
    {
        public Image top;
        public Image upperMiddle;
        public Image lowerMiddle;
        public Image bottom;
    }

    [Header("Result Cells - Five Reels, Four Rows Each")]
    [Tooltip("Assign the four visible Images on reel 1, in top-to-bottom server row order.")]
    [SerializeField] private ResultReelImages reel1ResultImages = new ResultReelImages();
    [SerializeField] private ResultReelImages reel2ResultImages = new ResultReelImages();
    [SerializeField] private ResultReelImages reel3ResultImages = new ResultReelImages();
    [SerializeField] private ResultReelImages reel4ResultImages = new ResultReelImages();
    [SerializeField] private ResultReelImages reel5ResultImages = new ResultReelImages();

    [Header("Win Animation Layer - SlotAnimHolderTest")]
    [Tooltip("Five columns with four row containers each, containing SpineAnimController children.")]
    [SerializeField] private Transform slotAnimHolder;

    [Header("Spine Assets - Symbol ID Order, 0 Through 11")]
    [SerializeField] private SkeletonDataAsset[] symbolSpineAssets = new SkeletonDataAsset[12];

    private readonly SpineAnimController[,] symbolControllers = new SpineAnimController[5, VisibleRowCount];
    private readonly RectTransform[,] animationCells = new RectTransform[5, VisibleRowCount];

    [System.Serializable]
    private sealed class SpinModeSettings
    {
        [Tooltip("Minimum full passes per reel. Server latency and anticipation can add passes.")]
        [Min(1)] public int resetsBeforeStopping;
        [Tooltip("Average UI units per second over a full pass. Flash easing varies the speed within each pass.")]
        [Min(1f)] public float movementSpeed;

        public SpinModeSettings(int resets, float speed)
        {
            resetsBeforeStopping = resets;
            movementSpeed = speed;
        }
    }

    [Header("Spin Modes")]
    [SerializeField] private SpinModeSettings normal = new SpinModeSettings(2, 1800f);
    [InspectorName("Fast (Turbo)"), SerializeField] private SpinModeSettings turbo = new SpinModeSettings(2, 2600f);
    [InspectorName("Skip (Quick)"), SerializeField] private SpinModeSettings quick = new SpinModeSettings(1, 5200f);

    [Header("Win Animation Settings")]
    [SerializeField] private float winPopDuration = 0.4f;
    [SerializeField] private int winPopRepeat = 3;


    [Header("Scatter Anticipation")]
    [Tooltip("Anticipation visuals for reels 2 through 5, in that order.")]
    [SerializeField] private GameObject[] scatterAnticipationObjects = new GameObject[4];
    [SerializeField, Min(0f)] private float scatterAnticipationDuration = 3f;
    [SerializeField, Min(0f)] private float scatterAnticipationSoundFadeDuration = 0.4f;


    [Header("Win Animation Settings")]
    [SerializeField] private float winAnimationDuration = 3.0f; // Total duration each win symbol animation plays
    [SerializeField] private float winSymbolLoopDuration = 1.5f;
    [SerializeField] private int winSymbolLoopCount = 3;
    [Tooltip("Delay before starting an individual symbol win animation.")]
    [SerializeField] private float winLineBoxToAnimationDelay = 0.05f;

    [Header("Symbol Info Card")]
    [SerializeField] private SymbolInfoCard symbolInfoCard;


    private List<Tween> spinTweens = new List<Tween>();
    private List<Tween> winTweens = new List<Tween>();
    private List<int> reelCycleCount = new List<int>();
    private readonly float[] reelCycleBaseDurations = new float[5];
    private readonly bool[] reelIsAnticipating = new bool[5];
    private readonly bool[] reelHasStopped = new bool[5];
    private List<List<int>> pendingResultMatrix;
    private readonly bool[] reelResultPrepared = new bool[5];
    private bool manualStopRequested;
    private Coroutine stopSpinCoroutine;
    private Coroutine winAnimationCoroutine;
    private Coroutine loopingWinSymbolsCoroutine;


    internal List<List<int>> currentDisplayMatrix;

    private bool isSpinning;

    #region Initialization

    private void Awake()
    {
        BuildSymbolSpriteArray();
        BindSpinStripImages();
        HasValidResultCells(true);
        BindResultAndAnimationLayer();
        HasValidWinBoxes(true);
        HideAllWinBoxes();
        InitializeReels();
    }

    private void Start()
    {
        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            BuildSymbolSpriteArray();
        }
        DisableAllOverlays();
        HideWinLinePresentation();
        StopAllScatterAnticipationVisuals();
        SetupSymbolButtons();
    }

    private void DisableAllOverlays()
    {
        StopAnimationOverlays();
        if (symbolInfoCard) symbolInfoCard.HideCard();
    }

    private void SetupSymbolButtons()
    {
        if (reelImagesList == null) return;
        for (int col = 0; col < reelImagesList.Count; col++)
        {
            for (int row = 0; row < VisibleRowCount; row++)
            {
                Image img = GetNormalResultImage(col, row);
                if (img != null)
                {
                    SymbolButtonHandler btnHandler = img.GetComponent<SymbolButtonHandler>();
                    if (btnHandler == null)
                    {
                        btnHandler = img.gameObject.AddComponent<SymbolButtonHandler>();
                    }
                    btnHandler.Init(col, row, this);
                }
            }
        }
    }

    private void BindSpinStripImages()
    {
        if (HasCompleteSpinStrips()) return;

        reelImagesList = new List<ReelImages>(5);
        int columnCount = reelTransforms == null ? 0 : Mathf.Min(5, reelTransforms.Length);

        for (int col = 0; col < columnCount; col++)
        {
            ReelImages strip = new ReelImages();
            strip.images.Clear();
            Transform reelTransform = reelTransforms[col];

            if (reelTransform != null)
            {
                for (int childIndex = 0; childIndex < reelTransform.childCount; childIndex++)
                {
                    Image image = reelTransform.GetChild(childIndex).GetComponent<Image>();
                    if (image != null) strip.images.Add(image);
                }
            }

            reelImagesList.Add(strip);
        }

        if (!HasCompleteSpinStrips())
            Debug.LogError("[SlotView] Each regular reel must contain at least 4 Image children.");
    }

    private bool HasCompleteSpinStrips()
    {
        if (reelImagesList == null || reelImagesList.Count < 5) return false;

        for (int col = 0; col < 5; col++)
        {
            ReelImages strip = reelImagesList[col];
            if (strip == null || strip.images == null || strip.images.Count < VisibleRowCount) return false;
            for (int index = 0; index < strip.images.Count; index++)
                if (strip.images[index] == null) return false;
        }

        return true;
    }

    private bool HasValidResultCells(bool logErrors)
    {
        if (reelTransforms == null || reelTransforms.Length != 5 || !HasCompleteSpinStrips())
        {
            if (logErrors)
                Debug.LogError("[SlotView] Assign five reel containers and their spin-strip Images before spinning.", this);
            return false;
        }

        HashSet<Image> assignedImages = new HashSet<Image>();
        for (int column = 0; column < 5; column++)
        {
            for (int row = 0; row < VisibleRowCount; row++)
            {
                Image image = GetNormalResultImage(column, row);
                if (image == null || reelTransforms[column] == null ||
                    !image.transform.IsChildOf(reelTransforms[column]) ||
                    !reelImagesList[column].images.Contains(image) || !assignedImages.Add(image))
                {
                    if (logErrors)
                        Debug.LogError($"[SlotView] Result cell on reel {column + 1}, row {row + 1} is missing, duplicated, or belongs to another reel. Assign Top through Bottom under Result Cells.", this);
                    return false;
                }
            }
        }

        return true;
    }

    private void BindResultAndAnimationLayer()
    {
        if (slotAnimHolder == null)
        {
            GameObject holderObject = GameObject.Find("SlotAnimHolderTest");
            if (holderObject != null) slotAnimHolder = holderObject.transform;
        }

        if (slotAnimHolder == null || slotAnimHolder.childCount != 5)
        {
            Debug.LogError("[SlotView] Assign SlotAnimHolderTest with five columns and four Spine rows per column.", this);
            return;
        }

        for (int col = 0; col < 5; col++)
        {
            Transform column = slotAnimHolder.GetChild(col);
            if (column.childCount != VisibleRowCount)
            {
                Debug.LogError($"[SlotView] Animation reel {col + 1} must have four row containers.", this);
                continue;
            }

            // Overlay positions follow the stopped result cells, not a separate layout.
            LayoutGroup layout = column.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;
            ContentSizeFitter fitter = column.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            for (int row = 0; row < VisibleRowCount; row++)
            {
                Transform cell = column.GetChild(row);
                animationCells[col, row] = cell as RectTransform;
                SpineAnimController controller = cell.GetComponentInChildren<SpineAnimController>(true);
                symbolControllers[col, row] = controller;
                if (controller == null) continue;

                controller.Stop();
                controller.gameObject.SetActive(false);
                cell.gameObject.SetActive(true);
                if (controller.SkeletonGraphic != null)
                    controller.SkeletonGraphic.raycastTarget = false;
            }
        }

        if (!HasCompleteResultLayer())
            Debug.LogError("[SlotView] Every animation row needs a RectTransform and a child SpineAnimController.", this);

        for (int symbolId = 0; symbolId < 12; symbolId++)
        {
            SkeletonDataAsset asset = GetSymbolSpineAsset(symbolId);
            Spine.Animation animation = asset != null ? asset.GetSkeletonData(false)?.FindAnimation("animation") : null;
            if (animation == null || animation.Duration <= 0f)
                Debug.LogError($"[SlotView] Symbol ID {symbolId} needs a Spine asset with a non-empty 'animation' clip. Its normal sprite will remain visible.", this);
        }
    }

    private bool HasCompleteResultLayer()
    {
        for (int col = 0; col < 5; col++)
        {
            for (int row = 0; row < VisibleRowCount; row++)
                if (symbolControllers[col, row] == null || animationCells[col, row] == null) return false;
        }

        return true;
    }

    internal void HideSymbolInfoCard()
    {
        if (symbolInfoCard != null) symbolInfoCard.HideCard();
    }

    internal void OnBetChanged()
    {
        if (symbolInfoCard != null && symbolInfoCard.gameObject.activeSelf)
        {
            symbolInfoCard.RefreshCard(gameManager);
        }
    }

    internal void OnSymbolClicked(int col, int row, RectTransform symbolRect)
    {
        if (isSpinning)
        {
            if (symbolInfoCard != null) symbolInfoCard.HideCard();
            return;
        }

        if (currentDisplayMatrix == null || col >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[col].Count)
        {
            return;
        }

        int symbolId = currentDisplayMatrix[col][row];
        if (symbolInfoCard != null)
        {
            symbolInfoCard.ShowCard(symbolId, col, row, symbolRect, gameManager);
        }
    }

    private SpineAnimController GetSpineController(int column, int row)
    {
        if (column < 0 || column >= 5 || row < 0 || row >= VisibleRowCount) return null;
        return symbolControllers[column, row];
    }

    private SkeletonDataAsset GetSymbolSpineAsset(int symbolId)
    {
        return symbolSpineAssets != null && symbolId >= 0 && symbolId < symbolSpineAssets.Length
            ? symbolSpineAssets[symbolId]
            : null;
    }

    private void AlignAnimationCell(int column, int row)
    {
        Image resultImage = GetNormalResultImage(column, row);
        RectTransform cell = animationCells[column, row];
        if (resultImage == null || cell == null) return;

        RectTransform resultRect = resultImage.rectTransform;
        Vector3 resultCenter = resultRect.TransformPoint(resultRect.rect.center);
        Vector3 animationCenter = cell.TransformPoint(cell.rect.center);
        cell.position += resultCenter - animationCenter;
    }

    private bool TryPrepareWinSymbol(int column, int row, out SpineAnimController controller)
    {
        controller = GetSpineController(column, row);
        if (controller == null || GetNormalResultImage(column, row) == null ||
            animationCells[column, row] == null || currentDisplayMatrix == null ||
            column >= currentDisplayMatrix.Count || currentDisplayMatrix[column] == null ||
            row >= currentDisplayMatrix[column].Count)
            return false;

        SkeletonDataAsset asset = GetSymbolSpineAsset(currentDisplayMatrix[column][row]);
        if (asset == null) return false;

        AlignAnimationCell(column, row);
        controller.gameObject.SetActive(true);
        if (!controller.BeginSynchronizedPlayback(asset))
        {
            controller.Stop();
            controller.gameObject.SetActive(false);
            SetNormalResultVisible(column, row, true);
            return false;
        }

        SetNormalResultVisible(column, row, false);
        return true;
    }

    private bool HasValidWinBoxes(bool logErrors)
    {
        if (winBoxImages == null || winBoxImages.Count != 5)
        {
            if (logErrors)
                Debug.LogError("[SlotView] Assign the five existing Winbox reels, with four row Images per reel.", this);
            return false;
        }

        HashSet<Image> assignedImages = new HashSet<Image>();
        for (int column = 0; column < 5; column++)
        {
            List<Image> images = winBoxImages[column]?.images;
            if (images == null || images.Count != VisibleRowCount)
            {
                if (logErrors)
                    Debug.LogError($"[SlotView] Winbox reel {column + 1} must contain Images 1 through 4 in row order.", this);
                return false;
            }

            for (int row = 0; row < VisibleRowCount; row++)
            {
                if (images[row] == null || !assignedImages.Add(images[row]))
                {
                    if (logErrors)
                        Debug.LogError($"[SlotView] Winbox on reel {column + 1}, row {row + 1} is missing or duplicated.", this);
                    return false;
                }
            }
        }

        return true;
    }

    private void SetWinBoxVisible(int column, int row, bool visible, Color? color = null)
    {
        if (winBoxImages == null || column < 0 || column >= winBoxImages.Count) return;

        List<Image> images = winBoxImages[column]?.images;
        if (images == null || row < 0 || row >= images.Count) return;

        Image box = images[row];
        if (box == null) return;

        box.raycastTarget = false;
        if (color.HasValue) box.color = color.Value;
        box.gameObject.SetActive(visible);
    }

    private void HideAllWinBoxes()
    {
        if (winBoxImages == null) return;

        foreach (ReelImages reel in winBoxImages)
        {
            if (reel?.images == null) continue;

            foreach (Image box in reel.images)
            {
                if (box == null) continue;
                box.raycastTarget = false;
                box.gameObject.SetActive(false);
            }
        }
    }

    private Color GetWinLineColor(int lineIndex)
    {
        if (winLineObjects == null || lineIndex < 0 || lineIndex >= winLineObjects.Count)
            return Color.white;

        GameObject line = winLineObjects[lineIndex];
        if (line == null) return Color.white;

        UILineConnector connector = line.GetComponent<UILineConnector>();
        if (connector != null) return connector.LineColor;

        Transform segment = line.transform.Find("Segment_0");
        Image image = segment != null ? segment.GetComponent<Image>() : null;
        return image != null ? image.color : Color.white;
    }

    private void BuildSymbolSpriteArray()
    {
        // Build the symbol sprite array from named sprite fields
        symbolSprites = new Sprite[12];
        symbolSprites[0] = symbolWildSprite;
        symbolSprites[1] = symbolScatterSprite;
        symbolSprites[2] = symbolMummySprite;
        symbolSprites[3] = symbolGoblinSprite;
        symbolSprites[4] = symbolVampireSprite;
        symbolSprites[5] = symbolWerewolfSprite;
        symbolSprites[6] = symbolASprite;
        symbolSprites[7] = symbolKSprite;
        symbolSprites[8] = symbolQSprite;
        symbolSprites[9] = symbolJSprite;
        symbolSprites[10] = symbolTenSprite;
        symbolSprites[11] = symbolNineSprite;

        // Validate
        for (int i = 0; i < symbolSprites.Length; i++)
        {
            if (symbolSprites[i] == null)
            {
                Debug.LogError($"[SlotView] Symbol sprite at index {i} is not assigned in inspector!");
            }
        }

    }

    private void InitializeReels()
    {
        int rowCount = VisibleRowCount;
        reelCycleCount.Clear();

        currentDisplayMatrix = new List<List<int>>();
        for (int col = 0; col < 5; col++)
        {
            SetReelY(col, ReelStartY);
            var defaultCol = new List<int>();
            for (int r = 0; r < rowCount; r++)
            {
                Image sceneImage = GetNormalResultImage(col, r);
                defaultCol.Add(GetSymbolId(sceneImage != null ? sceneImage.sprite : null));
            }
            currentDisplayMatrix.Add(defaultCol);
            reelCycleCount.Add(0);
        }
    }

    #endregion

    #region Symbol Display

    private void SetResultSymbols(int columnIndex, List<int> visibleSymbolIds)
    {
        if (reelImagesList == null || columnIndex < 0 || columnIndex >= reelImagesList.Count)
        {
            Debug.LogError($"SetResultSymbols: Invalid column index {columnIndex}");
            return;
        }

        int rowCount = VisibleRowCount;

        if (visibleSymbolIds == null || visibleSymbolIds.Count != rowCount)
        {
            Debug.LogError($"SetResultSymbols: Invalid visibleSymbolIds count {visibleSymbolIds?.Count}, expected {rowCount}");
            return;
        }

        for (int row = 0; row < rowCount; row++)
        {
            Image resultImage = GetNormalResultImage(columnIndex, row);
            if (resultImage != null)
            {
                int symbolId = visibleSymbolIds[row];
                resultImage.gameObject.SetActive(true);
                resultImage.enabled = true;
                resultImage.sprite = GetSymbolSprite(symbolId);
                resultImage.preserveAspect = true;
                resultImage.transform.localScale = Vector3.one;
                Color color = resultImage.color;
                resultImage.color = new Color(color.r, color.g, color.b, 1f);
            }
        }
    }

    private Image GetNormalResultImage(int column, int row)
    {
        ResultReelImages reel = column switch
        {
            0 => reel1ResultImages,
            1 => reel2ResultImages,
            2 => reel3ResultImages,
            3 => reel4ResultImages,
            4 => reel5ResultImages,
            _ => null
        };

        if (reel == null) return null;

        return row switch
        {
            0 => reel.top,
            1 => reel.upperMiddle,
            2 => reel.lowerMiddle,
            3 => reel.bottom,
            _ => null
        };
    }

    private void HideAnimationColumn(int column)
    {
        if (column < 0 || column >= 5) return;

        for (int row = 0; row < VisibleRowCount; row++)
        {
            ResetSymbolScale(column, row);
        }
    }

    private void SetNormalResultVisible(int column, int row, bool visible)
    {
        Image image = GetNormalResultImage(column, row);
        if (image == null) return;

        image.DOKill();
        Color color = image.color;
        image.color = new Color(color.r, color.g, color.b, 1f);
        // Keep the cell in the VerticalLayoutGroup when its overlay is shown.
        image.enabled = visible;
    }

    private void StopAnimationOverlays()
    {
        HideAllWinBoxes();
        for (int col = 0; col < 5; col++)
            HideAnimationColumn(col);
    }

    private Sprite GetSymbolSprite(int symbolId)
    {
        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            BuildSymbolSpriteArray();
        }

        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            Debug.LogError("[SlotView] symbolSprites array is null or empty after BuildSymbolSpriteArray!");
            return null;
        }

        // Validate symbolId range (0-11)
        if (symbolId < 0 || symbolId >= symbolSprites.Length)
        {
            Debug.LogWarning($"[SlotView] Invalid symbolId {symbolId}, using default sprite 0. Total sprites: {symbolSprites.Length}");
            return symbolSprites[0];
        }

        if (symbolSprites[symbolId] == null)
        {
            Debug.LogError($"[SlotView] Symbol sprite for ID {symbolId} is null!");
            return symbolSprites[0];
        }

        return symbolSprites[symbolId];
    }

    private int GetSymbolId(Sprite sprite)
    {
        if (sprite != null && symbolSprites != null)
        {
            for (int symbolId = 0; symbolId < symbolSprites.Length; symbolId++)
            {
                if (symbolSprites[symbolId] == sprite) return symbolId;
            }
        }

        return 0;
    }

    #endregion

    #region Spin Animation

    internal void StartSpin()
    {
        if (isSpinning || !HasValidResultCells(true)) return;
        if (symbolInfoCard != null) symbolInfoCard.HideCard();

        KillAllTweens();
        pendingResultMatrix = null;
        isSpinning = true;
        manualStopRequested = false;
        AudioManager.Instance?.PlaySpinningReel();
        DisableAllOverlays();

        for (int column = 0; column < 5; column++)
        {
            reelCycleCount[column] = 0;
            reelHasStopped[column] = false;
            reelResultPrepared[column] = false;
            StartReelCycle(column);
        }
    }


    private void StartReelCycle(int columnIndex)
    {
        if (!isSpinning || columnIndex >= reelTransforms.Length) return;

        RectTransform reel = reelTransforms[columnIndex] as RectTransform;
        if (reel == null)
        {
            Debug.LogError($"[SlotView] Reel {columnIndex + 1} needs a RectTransform.");
            return;
        }

        // A one-second base tween lets timeScale express the configured speed.
        const float baseCycleDuration = 1f;
        reelCycleBaseDurations[columnIndex] = baseCycleDuration;
        SetReelY(columnIndex, ReelStartY);
        Tween cycleTween = reel
            .DOAnchorPosY(ReelEndY, baseCycleDuration)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Flash)
            .OnUpdate(() => TryApplyPendingReelResult(columnIndex))
            .OnStepComplete(() =>
            {
                if (!isSpinning) return;

                // Handles a frame that skips over the 95% trigger.
                TryApplyPendingReelResult(columnIndex, force: true);
                reelCycleCount[columnIndex]++;
            });

        if (spinTweens.Count <= columnIndex)
            spinTweens.Add(cycleTween);
        else
            spinTweens[columnIndex] = cycleTween;

        ApplyCurrentSpinSpeedToReel(columnIndex);
    }

    private void TryApplyPendingReelResult(int columnIndex, bool force = false)
    {
        if (!isSpinning ||
            columnIndex < 0 ||
            columnIndex >= reelResultPrepared.Length ||
            reelResultPrepared[columnIndex] ||
            pendingResultMatrix == null ||
            columnIndex >= pendingResultMatrix.Count)
            return;

        List<int> symbols = pendingResultMatrix[columnIndex];
        if (symbols == null || symbols.Count != VisibleRowCount)
            return;

        if (!force)
        {
            RectTransform reel = reelTransforms[columnIndex] as RectTransform;
            if (reel == null) return;

            float progress = Mathf.InverseLerp(
                ReelStartY, ReelEndY, reel.anchoredPosition.y);

            if (progress < ResultAssignmentProgress)
                return;
        }

        SetResultSymbols(columnIndex, symbols);
        reelResultPrepared[columnIndex] = true;
    }

    private void SetReelY(int columnIndex, float y)
    {
        if (reelTransforms == null || columnIndex >= reelTransforms.Length) return;
        RectTransform reel = reelTransforms[columnIndex] as RectTransform;
        if (reel == null) return;
        Vector2 position = reel.anchoredPosition;
        position.y = y;
        reel.anchoredPosition = position;
    }

    private bool SkipStopWaits()
    {
        return manualStopRequested ||
            (gameManager != null && gameManager.currentSpinSpeed == SpinSpeed.QuickSpin);
    }

    private float GetStopTimeScale()
    {
        if (gameManager == null || gameManager.currentSpinSpeed != SpinSpeed.Turbo)
            return 1f;

        return Mathf.Max(1f, turbo.movementSpeed / Mathf.Max(1f, normal.movementSpeed));
    }

    private bool CanPlayScatterAnticipation()
    {
        return !manualStopRequested &&
            (gameManager == null || gameManager.currentSpinSpeed == SpinSpeed.Normal);
    }

    internal void RequestManualStop()
    {
        if (!isSpinning) return;
        manualStopRequested = true;
        StopAllScatterAnticipationVisuals();
    }

    private void OnValidate()
    {
        normal ??= new SpinModeSettings(2, 1800f);
        turbo ??= new SpinModeSettings(2, 2600f);
        quick ??= new SpinModeSettings(1, 5200f);
        foreach (SpinModeSettings settings in new[] { normal, turbo, quick })
        {
            settings.resetsBeforeStopping = Mathf.Max(1, settings.resetsBeforeStopping);
            settings.movementSpeed = Mathf.Max(1f, settings.movementSpeed);
        }
    }

    private void Update()
    {
        if (!isSpinning) return;
        // Inspector edits also take effect during an active spin.
        for (int column = 0; column < 5; column++)
            ApplyCurrentSpinSpeedToReel(column);
    }

    internal void OnSpinSpeedChanged(SpinSpeed speed)
    {
        if (!isSpinning) return;

        if (speed != SpinSpeed.Normal)
        {
            // Anticipation is Normal-only. Fast keeps its first-reel boundary wait;
            // Skip bypasses the boundary and spacing waits.
            StopAllScatterAnticipationVisuals();
        }

        for (int reelIndex = 0; reelIndex < reelCycleBaseDurations.Length; reelIndex++)
        {
            ApplyCurrentSpinSpeedToReel(reelIndex);
        }
    }

    private void ApplyCurrentSpinSpeedToReel(int reelIndex)
    {
        if (reelIndex < 0 || reelIndex >= reelCycleBaseDurations.Length ||
            reelIndex >= spinTweens.Count)
            return;

        Tween reelTween = spinTweens[reelIndex];
        float baseCycleDuration = reelCycleBaseDurations[reelIndex];
        if (baseCycleDuration <= 0f || reelTween == null || !reelTween.IsActive())
            return;

        SpinSpeed speed = gameManager != null
            ? gameManager.currentSpinSpeed
            : SpinSpeed.Normal;
        float targetCycleDuration = GetCycleDurationForSpeed(speed);
        float targetTimeScale = baseCycleDuration / targetCycleDuration;

        reelTween.timeScale = targetTimeScale;
    }

    private SpinModeSettings GetSpinSettings(SpinSpeed speed)
    {
        return speed switch
        {
            SpinSpeed.Turbo => turbo,
            SpinSpeed.QuickSpin => quick,
            _ => normal
        };
    }

    private float GetCycleDurationForSpeed(SpinSpeed speed)
    {
        return (ReelStartY - ReelEndY) /
            Mathf.Max(1f, GetSpinSettings(speed).movementSpeed);
    }

    #endregion

    #region Stop Spin

    internal void StopSpin(List<List<int>> resultMatrix, System.Action onComplete)
    {
        if (!isSpinning)
        {
            currentDisplayMatrix = resultMatrix;
            for (int col = 0; col < 5; col++)
            {
                SetResultSymbols(col, resultMatrix[col]);
                HideAnimationColumn(col);
                SetReelY(col, ReelStartY);
            }
            onComplete?.Invoke();
            return;
        }

        if (stopSpinCoroutine == null)
            stopSpinCoroutine = StartCoroutine(StopSpinSequence(resultMatrix, onComplete, false));
    }

    internal void StopSpinTogether(List<List<int>> resultMatrix, System.Action onComplete)
    {
        if (!isSpinning)
        {
            currentDisplayMatrix = resultMatrix;
            for (int col = 0; col < 5; col++)
            {
                SetResultSymbols(col, resultMatrix[col]);
                HideAnimationColumn(col);
                SetReelY(col, ReelStartY);
            }
            onComplete?.Invoke();
            return;
        }

        RequestManualStop();
        if (stopSpinCoroutine == null)
            stopSpinCoroutine = StartCoroutine(StopSpinSequence(resultMatrix, onComplete, true));
    }

    private IEnumerator StopSpinSequence(
        List<List<int>> resultMatrix,
        System.Action onComplete,
        bool stopTogether)
    {
        currentDisplayMatrix = resultMatrix;
        pendingResultMatrix = resultMatrix;
        if (stopTogether) RequestManualStop();

        int stoppedScatterCount = 0;
        for (int column = 0; column < 5; column++)
        {
            if (CanPlayScatterAnticipation() && stoppedScatterCount == ScatterCountForAnticipation)
                yield return PlayScatterAnticipation(column, scatterAnticipationDuration);

            // Normal requests each reel's next boundary after a pause.
            // Fast uses timed spacing after the first reel's boundary.
            // Skip and manual Stop advance to the next reel after one frame.
            yield return StopSingleReel(column, resultMatrix[column]);
            stoppedScatterCount += CountScatterSymbols(resultMatrix[column]);
        }

        // Landings can overlap because launch spacing can be less than 0.25 seconds.
        // Keep game completion tied to the last actual tween completion.
        yield return new WaitUntil(() => System.Array.TrueForAll(reelHasStopped, stopped => stopped));
        isSpinning = false;
        stopSpinCoroutine = null;
        AudioManager.Instance?.StopSpinningReel();
        onComplete?.Invoke();
    }

    private IEnumerator StopSingleReel(
        int columnIndex,
        List<int> targetSymbols)
    {
        int cycleWhenRequested = reelCycleCount[columnIndex];
        while (true)
        {
            SpinSpeed speed = gameManager != null
                ? gameManager.currentSpinSpeed
                : SpinSpeed.Normal;
            int requiredResets = Mathf.Max(1, GetSpinSettings(speed).resetsBeforeStopping);
            bool minimumMet = manualStopRequested ||
                reelCycleCount[columnIndex] >= requiredResets;
            bool boundaryReady = SkipStopWaits() ||
                (speed == SpinSpeed.Turbo && columnIndex > 0) ||
                reelCycleCount[columnIndex] > cycleWhenRequested;
            if (minimumMet && boundaryReady) break;

            yield return null;
        }

        spinTweens[columnIndex]?.Kill();
        // Prevent live movement-speed changes from scaling the landing tween.
        reelCycleBaseDurations[columnIndex] = 0f;
        SetReelY(columnIndex, ReelStartY + LandingStartOffset);
        TryApplyPendingReelResult(columnIndex, force: true);
        HideAnimationColumn(columnIndex);

        RectTransform reel = (RectTransform)reelTransforms[columnIndex];
        spinTweens[columnIndex] = reel
            .DOAnchorPosY(ReelStartY, LandingDuration / GetStopTimeScale())
            .SetEase(Ease.OutBack)
            .SetSpeedBased(false)
            .OnComplete(() =>
            {
                reelHasStopped[columnIndex] = true;
                AudioManager.Instance?.PlayReelStop();
                if (CountScatterSymbols(targetSymbols) > 0)
                    AudioManager.Instance?.PlayScatterInReel();
            });

        float elapsed = 0f;
        while (true)
        {
            yield return null;
            elapsed += Time.deltaTime;

            float stopInterval = gameManager != null &&
                gameManager.currentSpinSpeed == SpinSpeed.Turbo &&
                columnIndex < reelHasStopped.Length - 1
                ? GetCycleDurationForSpeed(SpinSpeed.Normal) * 0.5f
                : NormalStopSpacing / GetStopTimeScale();
            if (elapsed >= stopInterval || SkipStopWaits()) break;
        }
    }

    private int CountScatterSymbols(IReadOnlyList<int> symbols)
    {
        int scatterSymbolId = gameManager?.gameConfig?.scatterSymbolId ?? -1;
        if (scatterSymbolId < 0 || symbols == null) return 0;

        int scatterCount = 0;
        int visibleRowCount = Mathf.Min(VisibleRowCount, symbols.Count);
        for (int row = 0; row < visibleRowCount; row++)
        {
            if (symbols[row] == scatterSymbolId)
            {
                scatterCount++;
            }
        }
        return scatterCount;
    }

    private IEnumerator PlayScatterAnticipation(int reelIndex, float duration)
    {
        if (duration <= 0f || reelIndex <= 0 || reelIndex >= 5 || !CanPlayScatterAnticipation())
            yield break;

        reelIsAnticipating[reelIndex] = true;
        ApplyCurrentSpinSpeedToReel(reelIndex);

        GameObject anticipationVisual = GetScatterAnticipationVisual(reelIndex);
        ImageAnimation anticipationAnimation = anticipationVisual != null
            ? anticipationVisual.GetComponent<ImageAnimation>()
            : null;
        if (anticipationVisual != null)
        {
            anticipationVisual.SetActive(true);
            if (anticipationAnimation != null)
            {
                anticipationAnimation.doLoopAnimation = true;
                anticipationAnimation.StopAnimation();
                anticipationAnimation.StartAnimation();
            }
        }

        AudioManager.Instance?.PlayReelTension();

        float elapsed = 0f;
        float fadeDuration = Mathf.Min(scatterAnticipationSoundFadeDuration, duration);
        bool fadeStarted = false;
        while (elapsed < duration && reelIsAnticipating[reelIndex] && CanPlayScatterAnticipation())
        {
            float remaining = duration - elapsed;
            if (!fadeStarted && fadeDuration > 0f && remaining <= fadeDuration)
            {
                fadeStarted = true;
                AudioManager.Instance?.FadeOutReelTension(fadeDuration);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        StopScatterAnticipationVisual(reelIndex);
        AudioManager.Instance?.StopReelTension();
        reelIsAnticipating[reelIndex] = false;
        ApplyCurrentSpinSpeedToReel(reelIndex);
    }

    private GameObject GetScatterAnticipationVisual(int reelIndex)
    {
        int anticipationIndex = reelIndex - 1;
        return scatterAnticipationObjects != null &&
            anticipationIndex >= 0 && anticipationIndex < scatterAnticipationObjects.Length
                ? scatterAnticipationObjects[anticipationIndex]
                : null;
    }

    private void StopScatterAnticipationVisual(int reelIndex)
    {
        GameObject anticipationVisual = GetScatterAnticipationVisual(reelIndex);
        if (anticipationVisual == null) return;

        ImageAnimation animation = anticipationVisual.GetComponent<ImageAnimation>();
        if (animation != null)
        {
            animation.doLoopAnimation = false;
            animation.StopAnimation();
        }
        anticipationVisual.SetActive(false);
    }

    private void StopAllScatterAnticipationVisuals()
    {
        AudioManager.Instance?.StopReelTension();

        for (int reelIndex = 0; reelIndex < reelIsAnticipating.Length; reelIndex++)
        {
            reelIsAnticipating[reelIndex] = false;
        }

        if (scatterAnticipationObjects == null) return;

        for (int index = 0; index < scatterAnticipationObjects.Length; index++)
        {
            StopScatterAnticipationVisual(index + 1);
        }
    }


    #endregion

    #region Quick Spin

    internal void QuickStop(List<List<int>> resultMatrix, System.Action onComplete = null)
    {
        StopSpin(resultMatrix, onComplete);
    }

    #endregion

    #region Stop Symbol Animations

    internal void AnimateAllScatters(int loopCount)
    {
        if (currentDisplayMatrix == null) return;

        // Clear any individual hit animations before starting the collective one
        KillWinTweens();

        int actualScatterId = gameManager?.gameConfig != null ? gameManager.gameConfig.scatterSymbolId : -1;
        if (actualScatterId < 0) return;
        
        for (int col = 0; col < 5; col++)
        {
            for (int row = 0; row < currentDisplayMatrix[col].Count; row++)
            {
                if (currentDisplayMatrix[col][row] == actualScatterId)
                {
                    AnimateSymbolSingleLoop(col, row, loopCount);
                }
            }
        }
    }

    private void AnimateSymbolSingleLoop(int column, int row, int loopCount = 1)
    {
        Canvas.ForceUpdateCanvases();
        if (!TryPrepareWinSymbol(column, row, out SpineAnimController controller)) return;

        SetWinBoxVisible(column, row, true, GetWinLineColor(0));
        float loopDuration = Mathf.Max(0.01f, winSymbolLoopDuration);
        float duration = loopDuration * Mathf.Max(1, loopCount);
        float elapsedTime = 0f;
        Tween playback = DOTween.To(
                () => elapsedTime,
                value =>
                {
                    elapsedTime = value;
                    controller.SetSynchronizedTime(value, loopDuration);
                },
                duration,
                duration)
            .SetEase(Ease.Linear)
            .OnComplete(() => ResetSymbolScale(column, row));
        winTweens.Add(playback);
    }

    #endregion

    #region Win Line Animation

    internal void ShowWinLineAnimation(List<WinLine> winLines, System.Action onComplete)
    {
        // The first phase is the total-win presentation, so line artwork and
        // individual line amounts must start hidden.
        HideWinLinePresentation();

        if (winLines == null || winLines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        KillWinTweens();
        winAnimationCoroutine = StartCoroutine(PlayTwoPhaseWinLines(winLines, onComplete));
    }

    private IEnumerator PlayTwoPhaseWinLines(List<WinLine> winLines, System.Action onComplete)
    {
        const int multipleWinLineLoopCount = 2;
        const int winLinePresentationPassCount = 2;

        // ==========================================
        // PHASE 1: Show all winning icons at once
        // ==========================================
        HashSet<int> allWinPositions = new HashSet<int>();
        foreach (var winLine in winLines)
        {
            if (winLine.positions != null)
            {
                foreach (int flatIndex in winLine.positions)
                {
                    allWinPositions.Add(flatIndex);
                }
            }
        }

        Debug.Log($"[PlayTwoPhaseWinLines] Phase 1: Showing all {allWinPositions.Count} winning icons at once for {winLines.Count} win lines");

        // A free-spin trigger has another screen waiting after the win display.
        // Keep this animation finite, clean it up before invoking the callback,
        // and never enter the ordinary repeating win-line presentation.
        bool hasFreeSpinTrigger = gameManager != null &&
            gameManager.lastResult != null &&
            gameManager.lastResult.freeSpinData != null &&
            gameManager.lastResult.freeSpinData.isTriggered;

        if (hasFreeSpinTrigger)
        {
            yield return AnimateWinPositions(
                allWinPositions,
                1,
                false,
                null);

            HideAllWinLineTexts();
            KillWinTweens(false);
            winAnimationCoroutine = null;

            Debug.Log("[PlayTwoPhaseWinLines] Free-spin trigger animation complete. Continuing game flow.");
            onComplete?.Invoke();
            yield break;
        }

        List<WinLine> displayWinLines = new List<WinLine>();
        foreach (WinLine winLine in winLines)
        {
            if (winLine.positions != null && winLine.positions.Count > 0)
                displayWinLines.Add(winLine);
        }

        // Preserve the initial all-wins presentation for two complete symbol
        // loops before moving into the individual winning-line sequence.
        yield return AnimateWinPositions(
            allWinPositions,
            multipleWinLineLoopCount,
            false,
            null);

        HideAllWinLineTexts();
        HideWinLinePresentation();

        // Invoke onComplete immediately after Phase 1 so game logic (Free Spins / Autoplay / Win complete) can proceed
        onComplete?.Invoke();

        // Skip Phase 2 while the server-authoritative free-spin trigger is being processed.
        bool hasSpecialFeature = gameManager != null && gameManager.lastResult != null &&
            gameManager.lastResult.freeSpinData != null &&
            gameManager.lastResult.freeSpinData.isTriggered;

        bool skipPhase2 = (gameManager != null && (gameManager.isInFreeSpins || gameManager.isAutoPlaying)) || hasSpecialFeature;
        if (skipPhase2)
        {
            KillWinTweens(false);
            yield break;
        }

        if (displayWinLines.Count == 0)
        {
            KillWinTweens(false);
            yield break;
        }

        // ==========================================
        // PHASE 2: Keep one continuous winning-line sequence. The first two
        // passes include line artwork and payout text; later passes keep the
        // same symbol/WinBox order with that presentation hidden.
        // ==========================================
        int completedPresentationPasses = 0;
        while (true)
        {
            bool showLinePresentation = completedPresentationPasses < winLinePresentationPassCount;

            for (int lineIndex = 0; lineIndex < displayWinLines.Count; lineIndex++)
            {
                WinLine winLine = displayWinLines[lineIndex];
                HideAllWinLineTexts();
                HideWinLinePresentation();

                if (showLinePresentation)
                {
                    ShowWinLinePresentation(winLine);
                }

                yield return AnimateWinPositions(
                    winLine.positions,
                    multipleWinLineLoopCount,
                    winBoxLineIndex: showLinePresentation ? winLine.lineId : 0);
            }

            if (showLinePresentation)
            {
                completedPresentationPasses++;
                if (completedPresentationPasses >= winLinePresentationPassCount)
                {
                    HideAllWinLineTexts();
                    HideWinLinePresentation();
                }
            }
        }
    }

    private IEnumerator AnimateWinPositions(
        IEnumerable<int> flatPositions,
        int loopCountTarget,
        bool continueUntilNextSpin = false,
        System.Action onInitialLoopsComplete = null,
        ISet<int> positionsToKeepActive = null,
        int winBoxLineIndex = 0)
    {
        if (flatPositions == null) yield break;

        int rowLimit = VisibleRowCount;
        loopCountTarget = Mathf.Max(1, loopCountTarget);
        float loopDuration = Mathf.Max(0.01f, winSymbolLoopDuration);

        List<SpineAnimController> activeAnims = new List<SpineAnimController>();
        List<Vector2Int> activePositions = new List<Vector2Int>();
        List<int> activeFlatIndices = new List<int>();
        HashSet<int> preparedPositions = new HashSet<int>();
        Color winBoxColor = GetWinLineColor(winBoxLineIndex);
        Canvas.ForceUpdateCanvases();

        foreach (int flatIndex in flatPositions)
        {
            int row = flatIndex / 5;
            int col = flatIndex % 5;

            if (flatIndex < 0 || col >= 5 || row >= rowLimit || !preparedPositions.Add(flatIndex)) continue;
            if (!TryPrepareWinSymbol(col, row, out SpineAnimController controller)) continue;

            activeAnims.Add(controller);
            activePositions.Add(new Vector2Int(col, row));
            activeFlatIndices.Add(flatIndex);
        }

        // Activate every position before advancing the shared clock so all
        // symbols and WinBoxes begin on the same rendered frame.
        for (int index = 0; index < activeAnims.Count; index++)
        {
            Vector2Int position = activePositions[index];
            SetWinBoxVisible(position.x, position.y, true, winBoxColor);
        }

        if (activeAnims.Count > 0)
        {
            float phaseDuration = loopDuration * loopCountTarget;
            float elapsedTime = 0f;

            while (elapsedTime < phaseDuration)
            {
                for (int index = 0; index < activeAnims.Count; index++)
                {
                    activeAnims[index].SetSynchronizedTime(elapsedTime, loopDuration);
                }

                yield return null;
                elapsedTime += Time.deltaTime;
            }

            // Put every animation on the exact shared boundary in the same frame.
            for (int index = 0; index < activeAnims.Count; index++)
            {
                activeAnims[index].SetSynchronizedTime(phaseDuration, loopDuration);
            }
            elapsedTime = phaseDuration;

            onInitialLoopsComplete?.Invoke();

            if (continueUntilNextSpin)
            {
                while (true)
                {
                    yield return null;
                    elapsedTime += Time.deltaTime;

                    for (int index = 0; index < activeAnims.Count; index++)
                    {
                        activeAnims[index].SetSynchronizedTime(elapsedTime, loopDuration);
                    }
                }
            }

            for (int i = 0; i < activeAnims.Count; i++)
            {
                if (positionsToKeepActive != null && positionsToKeepActive.Contains(activeFlatIndices[i]))
                    continue;

                Vector2Int position = activePositions[i];
                ResetSymbolScale(position.x, position.y);
            }
        }
        else
        {
            yield return new WaitForSeconds(loopDuration * loopCountTarget);
            onInitialLoopsComplete?.Invoke();
        }
    }

    private void ShowWinLineTextOnIcon(int col, int row, double winAmount)
    {
        Image symbolImage = GetNormalResultImage(col, row);
        if (symbolImage == null) return;

        Transform textTransform = symbolImage.transform.Find("WinLineText");
        if (textTransform != null)
        {
            var tmpText = textTransform.GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = winAmount.ToString("0.###");
            }
            AnimateTextScaleAppear(textTransform);
        }
    }

    private void HideAllWinLineTexts()
    {
        for (int col = 0; col < 5; col++)
        {
            for (int row = 0; row < VisibleRowCount; row++)
            {
                Image image = GetNormalResultImage(col, row);
                if (image != null)
                {
                    Transform textTransform = image.transform.Find("WinLineText");
                    if (textTransform != null)
                    {
                        textTransform.DOKill();
                        textTransform.localScale = Vector3.one;
                        textTransform.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    private void ShowWinLinePresentation(WinLine winLine)
    {
        HideWinLinePresentation();
        if (winLine == null || winLine.positions == null || winLine.positions.Count == 0) return;

        // Server lineIndex is zero-based, matching Line1 through Line40 in this list.
        int lineIndex = winLine.lineId;
        if (lineIndex >= 0 && lineIndex < winLineObjects.Count && winLineObjects[lineIndex] != null)
        {
            winLineObjects[lineIndex].SetActive(true);
        }

        int amountRow = GetWinAmountRow(winLine);
        TMPro.TMP_Text amountText = amountRow switch
        {
            0 => winAmountTopText,
            1 => winAmountMidText,
            2 => winAmountLowerMidText,
            _ => winAmountBottomText
        };

        if (amountText != null)
        {
            string amount = winLine.winAmount.ToString(
                "0.###",
                System.Globalization.CultureInfo.InvariantCulture);

            var builder = new System.Text.StringBuilder(amount.Length * 11);

            foreach (char character in amount)
            {
                if (character >= '0' && character <= '9')
                    builder.Append("<sprite=").Append(character - '0').Append('>');
                else if (character == '.')
                    builder.Append("<sprite=10>");
            }

            if (amountText.spriteAsset == null && winAmountTopText != null)
                amountText.spriteAsset = winAmountTopText.spriteAsset;

            amountText.richText = true;
            amountText.text = builder.ToString();
            amountText.gameObject.SetActive(true);
        }
    }

    private int GetWinAmountRow(WinLine winLine)
    {
        int centerReelIndex = gameManager != null && gameManager.gameConfig != null
            ? gameManager.gameConfig.reelCount / 2
            : 2;

        // Use the complete configured payline rather than only the winning
        // symbols. A partial win can stop before the centre reel, while its
        // line still crosses Top, Mid, or Bottom there.
        List<List<int>> paylines = gameManager?.gameConfig?.paylines;
        if (paylines != null &&
            winLine.lineId >= 0 && winLine.lineId < paylines.Count &&
            paylines[winLine.lineId] != null &&
            centerReelIndex >= 0 && centerReelIndex < paylines[winLine.lineId].Count)
        {
            return Mathf.Clamp(paylines[winLine.lineId][centerReelIndex], 0, VisibleRowCount - 1);
        }

        // Safe fallback for incomplete configuration data.
        foreach (int flatIndex in winLine.positions)
        {
            if (flatIndex % 5 == centerReelIndex)
            {
                return Mathf.Clamp(flatIndex / 5, 0, VisibleRowCount - 1);
            }
        }

        return Mathf.Clamp(winLine.positions[0] / 5, 0, VisibleRowCount - 1);
    }

    private void HideWinLinePresentation()
    {
        if (winLineObjects != null)
        {
            foreach (GameObject lineObject in winLineObjects)
            {
                if (lineObject != null) lineObject.SetActive(false);
            }
        }

        if (winAmountTopText) winAmountTopText.gameObject.SetActive(false);
        if (winAmountMidText) winAmountMidText.gameObject.SetActive(false);
        if (winAmountLowerMidText) winAmountLowerMidText.gameObject.SetActive(false);
        if (winAmountBottomText) winAmountBottomText.gameObject.SetActive(false);
    }

    private void AnimateTextScaleAppear(Transform textTransform, float popScale = 1.2f, float durationUp = 0.15f, float durationDown = 0.10f)
    {
        if (textTransform == null) return;
        textTransform.DOKill();
        textTransform.localScale = Vector3.zero;
        textTransform.gameObject.SetActive(true);

        Sequence seq = DOTween.Sequence();
        seq.Append(textTransform.DOScale(popScale, durationUp).SetEase(Ease.OutQuad));
        seq.Append(textTransform.DOScale(1.0f, durationDown).SetEase(Ease.InQuad));
        winTweens.Add(seq);
    }
    private void ResetSymbolScale(int col, int row)
    {
        SetWinBoxVisible(col, row, false);
        Image resultImage = GetNormalResultImage(col, row);
        if (resultImage != null)
        {
            resultImage.DOKill();
            resultImage.gameObject.SetActive(true);
            resultImage.transform.localScale = Vector3.one;
            // Restore alpha to full opacity
            Color c = resultImage.color;
            resultImage.color = new Color(c.r, c.g, c.b, 1f);
        }

        SpineAnimController controller = GetSpineController(col, row);
        if (controller != null)
        {
            controller.Stop();
            controller.gameObject.SetActive(false);
        }
        if (resultImage != null && currentDisplayMatrix != null && col < currentDisplayMatrix.Count &&
            currentDisplayMatrix[col] != null && row < currentDisplayMatrix[col].Count)
            resultImage.sprite = GetSymbolSprite(currentDisplayMatrix[col][row]);
        SetNormalResultVisible(col, row, true);
    }


    private void AnimateWinSymbol(int column, int row)
    {
        int loopCount = (gameManager != null && (gameManager.isInFreeSpins || gameManager.isAutoPlaying)) ? 1 : winSymbolLoopCount;
        Sequence sequence = DOTween.Sequence();
        if (winLineBoxToAnimationDelay > 0f)
            sequence.AppendInterval(winLineBoxToAnimationDelay);
        sequence.AppendCallback(() => AnimateSymbolSingleLoop(column, row, loopCount));
        winTweens.Add(sequence);
    }

    private void KillWinTweens(bool stopCoroutine = true)
    {
        HideAllWinBoxes();
        foreach (var tween in winTweens)
        {
            tween?.Kill();
        }
        winTweens.Clear();

        if (stopCoroutine && winAnimationCoroutine != null)
        {
            StopCoroutine(winAnimationCoroutine);
            winAnimationCoroutine = null;
        }

        if (stopCoroutine && loopingWinSymbolsCoroutine != null)
        {
            StopCoroutine(loopingWinSymbolsCoroutine);
            loopingWinSymbolsCoroutine = null;
        }

        StopAnimationOverlays();

        HideAllWinLineTexts();
        HideWinLinePresentation();
    }

    #endregion


    internal List<List<int>> GetCurrentDisplayMatrix()
    {
        return currentDisplayMatrix;
    }

    internal bool IsSpinning()
    {
        return isSpinning;
    }


    private void KillAllTweens()
    {
        if (stopSpinCoroutine != null)
        {
            StopCoroutine(stopSpinCoroutine);
            stopSpinCoroutine = null;
        }
        StopAllScatterAnticipationVisuals();

        foreach (var tween in spinTweens)
        {
            tween?.Kill();
        }
        spinTweens.Clear();

        for (int reelIndex = 0; reelIndex < reelCycleBaseDurations.Length; reelIndex++)
        {
            reelCycleBaseDurations[reelIndex] = 0f;
        }

        KillWinTweens();
    }

    #region Cleanup

    private void OnDestroy()
    {
        KillAllTweens();
    }

    #endregion
}

[System.Serializable]
public class ReelImages
{
    public List<Image> images = new List<Image>(16);
}
