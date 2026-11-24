using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using DG.Tweening;

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

    [Header("Win Animation Sprite Arrays")]
    [Tooltip("Animation sprites for the Wild symbol (ID 0)")]
    [SerializeField] private List<Sprite> animSpritesWild;
    [Tooltip("Animation sprites for the Scatter symbol (ID 1)")]
    [FormerlySerializedAs("animSpritesUSpin"), SerializeField] private List<Sprite> animSpritesScatter;
    [Tooltip("Optional frames. An empty list keeps this game's normal sprite with its existing winbox outline.")]
    [SerializeField] private List<Sprite> animSpritesMummy;
    [SerializeField] private List<Sprite> animSpritesGoblin;
    [SerializeField] private List<Sprite> animSpritesVampire;
    [SerializeField] private List<Sprite> animSpritesWerewolf;
    [Tooltip("Animation sprites for the A symbol (ID 6)")]
    [SerializeField] private List<Sprite> animSpritesA;
    [Tooltip("Animation sprites for the K symbol (ID 7)")]
    [SerializeField] private List<Sprite> animSpritesK;
    [Tooltip("Animation sprites for the Q symbol (ID 8)")]
    [SerializeField] private List<Sprite> animSpritesQ;
    [Tooltip("Animation sprites for the J symbol (ID 9)")]
    [SerializeField] private List<Sprite> animSpritesJ;
    [Tooltip("Animation sprites for the 10 symbol (ID 10)")]
    [SerializeField] private List<Sprite> animSpritesTen;
    [Tooltip("Animation sprites for the 9 symbol (ID 11)")]
    [SerializeField] private List<Sprite> animSpritesNine;

    // Internal array of animation sprite lists
    private List<Sprite>[] animationSpriteArrays;

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

    [Header("Win Animation Layer - SlotAnimHolder")]
    [Tooltip("Optional. When empty, SlotView finds SlotAnimHolder and binds its 5 columns x 4 rows automatically.")]
    [SerializeField] private Transform slotAnimHolder;
    [Tooltip("Animation-only Images for each column. They stay hidden until the matching normal result symbol wins.")]
    [FormerlySerializedAs("resultImagesList"), SerializeField] private List<ReelImages> slotAnimationImages = new List<ReelImages>(5);

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
    [Tooltip("Delay between enabling winBox overlay and starting the ImageAnimation - for sync timing")]
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
        if (!HasCompleteResultLayer())
        {
            if (slotAnimHolder == null)
            {
                GameObject holderObject = GameObject.Find("SlotAnimHolder");
                if (holderObject != null) slotAnimHolder = holderObject.transform;
            }

            if (slotAnimHolder != null)
            {
                slotAnimationImages = new List<ReelImages>(5);
                int columnCount = Mathf.Min(5, slotAnimHolder.childCount);

                for (int col = 0; col < columnCount; col++)
                {
                    Transform column = slotAnimHolder.GetChild(col);
                    ReelImages resultColumn = new ReelImages();
                    resultColumn.images.Clear();

                    int rowCount = Mathf.Min(VisibleRowCount, column.childCount);
                    for (int row = 0; row < rowCount; row++)
                    {
                        resultColumn.images.Add(column.GetChild(row).GetComponent<Image>());
                    }

                    slotAnimationImages.Add(resultColumn);
                }
            }
        }

        if (!HasCompleteResultLayer())
        {
            Debug.LogError("[SlotView] SlotAnimHolder must contain 5 columns with 4 Image children in each column.");
        }
    }

    private bool HasCompleteResultLayer()
    {
        if (slotAnimationImages == null || slotAnimationImages.Count < 5) return false;

        for (int col = 0; col < 5; col++)
        {
            if (slotAnimationImages[col] == null || slotAnimationImages[col].images == null || slotAnimationImages[col].images.Count < VisibleRowCount)
                return false;

            for (int row = 0; row < VisibleRowCount; row++)
                if (slotAnimationImages[col].images[row] == null) return false;
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

    private GameObject GetAnimationObject(int column, int row)
    {
        if (slotAnimationImages == null || column < 0 || column >= slotAnimationImages.Count) return null;

        ReelImages animationColumn = slotAnimationImages[column];
        if (animationColumn == null || animationColumn.images == null || row < 0 || row >= animationColumn.images.Count)
            return null;

        Image animationImage = animationColumn.images[row];
        return animationImage != null ? animationImage.gameObject : null;
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

    private List<Sprite> GetLoopingSymbolFrames(int symbolId)
    {
        if (animationSpriteArrays == null || symbolId < 0 || symbolId >= animationSpriteArrays.Length)
            return null;

        List<Sprite> frames = animationSpriteArrays[symbolId];
        // Until matching Jack O'Lantern animation frames are supplied, show the
        // correct base symbol with its existing winbox outline.
        if (frames == null || frames.Count == 0)
            return new List<Sprite> { GetSymbolSprite(symbolId) };

        // The serialized animation lists begin with the normal stopped symbol.
        // Showing that duplicate base sprite at every wrap makes a continuous
        // loop look as though it briefly switched off. Exclude it at runtime;
        // the source list and Inspector configuration remain unchanged.
        if (frames.Count > 1 && symbolSprites != null && symbolId < symbolSprites.Length && frames[0] == symbolSprites[symbolId])
            return frames.GetRange(1, frames.Count - 1);

        return frames;
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

        // Build the animation sprite arrays
        animationSpriteArrays = new List<Sprite>[12];
        animationSpriteArrays[0] = animSpritesWild;
        animationSpriteArrays[1] = animSpritesScatter;
        animationSpriteArrays[2] = animSpritesMummy;
        animationSpriteArrays[3] = animSpritesGoblin;
        animationSpriteArrays[4] = animSpritesVampire;
        animationSpriteArrays[5] = animSpritesWerewolf;
        animationSpriteArrays[6] = animSpritesA;
        animationSpriteArrays[7] = animSpritesK;
        animationSpriteArrays[8] = animSpritesQ;
        animationSpriteArrays[9] = animSpritesJ;
        animationSpriteArrays[10] = animSpritesTen;
        animationSpriteArrays[11] = animSpritesNine;
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

    private void SetAnimationColumnVisible(int column, bool visible)
    {
        if (slotAnimationImages == null || column < 0 || column >= slotAnimationImages.Count) return;
        ReelImages resultColumn = slotAnimationImages[column];
        if (resultColumn == null || resultColumn.images == null) return;

        foreach (Image image in resultColumn.images)
        {
            if (image == null) continue;
            image.DOKill();
            Color color = image.color;
            image.color = new Color(color.r, color.g, color.b, 1f);
            image.gameObject.SetActive(visible);
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
        if (slotAnimationImages == null) return;

        for (int col = 0; col < slotAnimationImages.Count; col++)
        {
            ReelImages resultColumn = slotAnimationImages[col];
            if (resultColumn == null || resultColumn.images == null) continue;

            for (int row = 0; row < resultColumn.images.Count; row++)
            {
                Image image = resultColumn.images[row];
                if (image == null) continue;

                ImageAnimation animation = image.GetComponent<ImageAnimation>();
                if (animation != null)
                {
                    animation.onLoopComplete = null;
                    animation.doLoopAnimation = false;
                }

                image.gameObject.SetActive(false);
                animation?.StopAnimation();
                SetNormalResultVisible(col, row, true);
            }
        }
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
                SetAnimationColumnVisible(col, false);
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
                SetAnimationColumnVisible(col, false);
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
        SetAnimationColumnVisible(columnIndex, false);

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
        Image symbolImage = GetNormalResultImage(column, row);
        if (symbolImage == null) return;

        var animGO = GetAnimationObject(column, row);
        if (animGO == null) return;

        ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
        if (imageAnim == null) return;

        int symbolId = currentDisplayMatrix[column][row];
        if (symbolId < 0 || symbolId >= animationSpriteArrays.Length) return;

        List<Sprite> animSprites = GetLoopingSymbolFrames(symbolId);
        if (animSprites == null || animSprites.Count == 0) return;

        imageAnim.textureArray = animSprites;
        imageAnim.SetLoopDuration(winSymbolLoopDuration);
        imageAnim.doLoopAnimation = true;

        Sequence seq = DOTween.Sequence();
        
        seq.AppendCallback(() => {
            SetNormalResultVisible(column, row, false);
            animGO.SetActive(true);
            SetWinBoxVisible(column, row, true, GetWinLineColor(0));
            imageAnim.StartAnimation();
        });

        seq.AppendInterval(winSymbolLoopDuration * loopCount);

        seq.AppendCallback(() => {
            if (imageAnim != null)
            {
                imageAnim.doLoopAnimation = false;
                imageAnim.StopAnimation();
            }
            if (animGO != null) animGO.SetActive(false);
            SetWinBoxVisible(column, row, false);
            SetNormalResultVisible(column, row, true);
        });

        winTweens.Add(seq);
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

        int rowLimit = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : VisibleRowCount;
        loopCountTarget = Mathf.Max(1, loopCountTarget);

        List<ImageAnimation> activeAnims = new List<ImageAnimation>();
        List<GameObject> activeAnimationObjects = new List<GameObject>();
        List<Vector2Int> activePositions = new List<Vector2Int>();
        List<int> activeFlatIndices = new List<int>();
        Color winBoxColor = GetWinLineColor(winBoxLineIndex);

        foreach (int flatIndex in flatPositions)
        {
            int row = flatIndex / 5;
            int col = flatIndex % 5;

            if (col < 0 || col >= 5 || row < 0 || row >= rowLimit) continue;

            Image symbolImage = GetNormalResultImage(col, row);
            if (symbolImage == null) continue;

            var animGO = GetAnimationObject(col, row);
            if (animGO == null) continue;

            ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
            if (imageAnim == null) continue;

            if (col >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[col].Count) continue;
            int symbolId = currentDisplayMatrix[col][row];
            if (symbolId < 0 || symbolId >= animationSpriteArrays.Length) continue;

            List<Sprite> animSprites = GetLoopingSymbolFrames(symbolId);
            if (animSprites == null || animSprites.Count == 0) continue;

            imageAnim.textureArray = animSprites;
            imageAnim.SetLoopDuration(winSymbolLoopDuration);
            imageAnim.doLoopAnimation = true;
            imageAnim.onLoopComplete = null;

            activeAnims.Add(imageAnim);
            activeAnimationObjects.Add(animGO);
            activePositions.Add(new Vector2Int(col, row));
            activeFlatIndices.Add(flatIndex);
        }

        // Activate every position before advancing the shared clock so all
        // symbols and WinBoxes begin on the same rendered frame.
        for (int index = 0; index < activeAnims.Count; index++)
        {
            Vector2Int position = activePositions[index];
            SetNormalResultVisible(position.x, position.y, false);
            activeAnimationObjects[index].SetActive(true);
            SetWinBoxVisible(position.x, position.y, true, winBoxColor);
        }

        if (activeAnims.Count > 0)
        {
            for (int index = 0; index < activeAnims.Count; index++)
            {
                activeAnims[index].BeginSynchronizedPlayback();
            }

            float phaseDuration = winSymbolLoopDuration * loopCountTarget;
            float elapsedTime = 0f;

            while (elapsedTime < phaseDuration)
            {
                for (int index = 0; index < activeAnims.Count; index++)
                {
                    activeAnims[index].SetSynchronizedTime(elapsedTime, winSymbolLoopDuration);
                }

                yield return null;
                elapsedTime += Time.deltaTime;
            }

            // Put every animation on the exact shared boundary in the same frame.
            for (int index = 0; index < activeAnims.Count; index++)
            {
                activeAnims[index].SetSynchronizedTime(phaseDuration, winSymbolLoopDuration);
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
                        activeAnims[index].SetSynchronizedTime(elapsedTime, winSymbolLoopDuration);
                    }
                }
            }

            // Stop the manual clock for every group together. Shared groups stay
            // visible on frame zero and are picked up by the next line immediately.
            for (int index = 0; index < activeAnims.Count; index++)
            {
                activeAnims[index].EndSynchronizedPlayback();
            }

            for (int i = 0; i < activeAnims.Count; i++)
            {
                if (positionsToKeepActive != null && positionsToKeepActive.Contains(activeFlatIndices[i]))
                    continue;

                ImageAnimation imageAnim = activeAnims[i];
                imageAnim.onLoopComplete = null;
                imageAnim.doLoopAnimation = false;
                activeAnimationObjects[i].SetActive(false);
                imageAnim.StopAnimation();

                Vector2Int position = activePositions[i];
                SetWinBoxVisible(position.x, position.y, false);
                SetNormalResultVisible(position.x, position.y, true);
            }
        }
        else
        {
            yield return new WaitForSeconds(winSymbolLoopDuration * loopCountTarget);
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
            resultImage.transform.localScale = Vector3.one;
            // Restore alpha to full opacity
            Color c = resultImage.color;
            resultImage.color = new Color(c.r, c.g, c.b, 1f);
        }

        // Also ensure the corresponding animation object is disabled
        var animGO = GetAnimationObject(col, row);
        if (animGO != null)
        {
            ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
            if (imageAnim != null)
            {
                if (imageAnim.rendererDelegate != null) imageAnim.rendererDelegate.DOKill();
                imageAnim.doLoopAnimation = false;
                imageAnim.StopAnimation();
            }
            if (resultImage != null && currentDisplayMatrix != null && col < currentDisplayMatrix.Count && row < currentDisplayMatrix[col].Count)
                resultImage.sprite = GetSymbolSprite(currentDisplayMatrix[col][row]);
            animGO.SetActive(false);
            SetNormalResultVisible(col, row, true);
        }
    }


    private void AnimateWinSymbol(int column, int row)
    {
        Image symbolImage = GetNormalResultImage(column, row);
        if (symbolImage == null)
        {
            Debug.LogError($"[AnimateWinSymbol] Result image is NULL at col: {column}, row: {row}");
            return;
        }


        // Get the animation GameObject for this position
        var animGO = GetAnimationObject(column, row);
        if (animGO == null)
        {
            Debug.LogError($"[AnimateWinSymbol] Animation GameObject is NULL at col: {column}, row: {row}");
            return;
        }

        // Get the ImageAnimation component
        ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
        if (imageAnim == null)
        {
            Debug.LogError($"[AnimateWinSymbol] ImageAnimation component not found on animation object at col: {column}, row: {row}");
            return;
        }

        // Get the current symbol ID at this position
        if (column >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[column].Count)
        {
            Debug.LogError($"[AnimateWinSymbol] Invalid matrix position col: {column}, row: {row}");
            return;
        }

        int symbolId = currentDisplayMatrix[column][row];
        
        // Validate symbolId
        if (symbolId < 0 || symbolId >= animationSpriteArrays.Length)
        {
            Debug.LogError($"[AnimateWinSymbol] Invalid symbolId {symbolId} at col: {column}, row: {row}");
            return;
        }

        // Get the animation sprite array for this symbol
        List<Sprite> animSprites = GetLoopingSymbolFrames(symbolId);
        if (animSprites == null || animSprites.Count == 0)
        {
            return;
        }

        // Set the sprite array on the ImageAnimation component
        imageAnim.textureArray = animSprites;
        imageAnim.SetLoopDuration(winSymbolLoopDuration);
        imageAnim.doLoopAnimation = true;

        Sequence seq = DOTween.Sequence();
        
        seq.AppendCallback(() => {
            SetNormalResultVisible(column, row, false);
            animGO.SetActive(true);
            SetWinBoxVisible(column, row, true, GetWinLineColor(0));
        });

        if (winLineBoxToAnimationDelay > 0)
        {
            seq.AppendInterval(winLineBoxToAnimationDelay);
        }

        seq.AppendCallback(() => {
            imageAnim.StartAnimation();
        });

        int loopCount = (gameManager != null && (gameManager.isInFreeSpins || gameManager.isAutoPlaying)) ? 1 : winSymbolLoopCount;
        seq.AppendInterval(winSymbolLoopDuration * loopCount);

        seq.AppendCallback(() => {
            if (imageAnim != null)
            {
                imageAnim.doLoopAnimation = false;
                imageAnim.StopAnimation();
            }
            if (animGO != null) animGO.SetActive(false);
            SetWinBoxVisible(column, row, false);
            SetNormalResultVisible(column, row, true);
        });

        winTweens.Add(seq);
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

        // Stop all win animations and disable animation GameObjects
        if (slotAnimationImages != null)
        {
            foreach (ReelImages column in slotAnimationImages)
            {
                if (column?.images != null)
                {
                    foreach (Image animationImage in column.images)
                    {
                        GameObject animGO = animationImage != null ? animationImage.gameObject : null;
                        if (animGO != null)
                        {
                            ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
                            if (imageAnim != null)
                            {
                                imageAnim.onLoopComplete = null;
                                imageAnim.doLoopAnimation = false;
                                if (imageAnim.rendererDelegate != null)
                                {
                                    imageAnim.rendererDelegate.DOKill();
                                    Color ac = imageAnim.rendererDelegate.color;
                                    imageAnim.rendererDelegate.color = new Color(ac.r, ac.g, ac.b, 1f);
                                }
                            }
                            animGO.SetActive(false);
                            imageAnim?.StopAnimation();
                        }
                    }
                }
            }
        }

        HideAllWinLineTexts();
        HideWinLinePresentation();

        // Restore the normal result cells that may have been hidden while their
        // matching SlotAnimHolder animation was playing.
        for (int col = 0; col < 5; col++)
        {
            for (int row = 0; row < VisibleRowCount; row++)
            {
                Image image = GetNormalResultImage(col, row);
                if (image != null)
                {
                    image.DOKill();
                    image.transform.localScale = Vector3.one;
                    if (currentDisplayMatrix != null && col < currentDisplayMatrix.Count && row < currentDisplayMatrix[col].Count)
                        image.sprite = GetSymbolSprite(currentDisplayMatrix[col][row]);
                    Color c = image.color;
                    image.color = new Color(c.r, c.g, c.b, 1f);
                    image.gameObject.SetActive(true);
                    image.enabled = true;
                }
            }
        }
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
