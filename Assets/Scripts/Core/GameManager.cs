using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour that acts as the central game coordinator.
///
/// Responsibilities:
///   - Registers bubble sprites in BubbleSpriteRegistry at startup
///   - Loads/saves progress via PlayerPrefs (10-bit bitmask for numbers 0-9)
///   - Instantiates / destroys LetterPuzzle prefabs as the player advances
///   - Exposes StartGame() and SelectNumber() for UI/Lobby to call
///
/// Flow: LobbyManager calls StartGame() → gameplay begins.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static event System.Action OnGameStartedEvent;
    public static event System.Action OnReturnedToLobbyEvent;

    public static GameManager Instance { get; private set; }

    [Header("Number Data (index 0-9)")]
    [Tooltip("10 NumberData assets for numbers 0-9 in order.")]
    public LetterData[] numbers;

    [Header("Bubble Sprites — unpopped (raised) state")]
    public Sprite blueUnpopped;
    public Sprite greenUnpopped;
    public Sprite pinkUnpopped;
    public Sprite redUnpopped;
    public Sprite yellowUnpopped;

    [Header("Bubble Sprites — popped (depressed) state")]
    public Sprite bluePopped;
    public Sprite greenPopped;
    public Sprite pinkPopped;
    public Sprite redPopped;
    public Sprite yellowPopped;

    [Header("Prefabs")]
    public LetterPuzzle letterPuzzlePrefab;
    public Bubble bubblePrefab;

    [Header("Scene References")]
    public Transform letterPuzzleArea;
    public UIManager uiManager;

    [Header("Animation Setup")]
    public GameObject jiggleLetterObject;

    private SpriteRenderer _jiggleLetterRenderer;
    private ParticleSystem[] _jiggleParticles;
    private bool _animationInitialized;

    private const string PREFS_NUMBERS = "ProgressNumbers";
    private const int NUMBER_COUNT = 10;

    private bool[] _completedFlags = new bool[NUMBER_COUNT];
    private int _currentIndex;
    private LetterPuzzle _currentPuzzle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RegisterBubbleSprites();
    }

    public void ReturnToLobby()
    {
        StopAllCoroutines();

        if (_currentPuzzle != null)
        {
            _currentPuzzle.OnLetterCompleted -= HandleNumberCompleted;
            Destroy(_currentPuzzle.gameObject);
            _currentPuzzle = null;
        }

        if (jiggleLetterObject != null)
            jiggleLetterObject.SetActive(false);

        OnReturnedToLobbyEvent?.Invoke();
    }

    public void StartGame()
    {
        LoadProgress();
        LoadNumber(_currentIndex);

        if (uiManager != null)
            uiManager.RefreshAll(_completedFlags, _currentIndex);

        OnGameStartedEvent?.Invoke();
    }

    public void SelectNumber(int index)
    {
        index = Mathf.Clamp(index, 0, NUMBER_COUNT - 1);
        _currentIndex = index;
        LoadNumber(index);

        if (uiManager != null)
            uiManager.SetActiveHighlight(index);
    }

    private void LoadNumber(int index)
    {
        if (_currentPuzzle != null)
        {
            _currentPuzzle.OnLetterCompleted -= HandleNumberCompleted;
            Destroy(_currentPuzzle.gameObject);
            _currentPuzzle = null;
        }

        if (numbers == null || index < 0 || index >= numbers.Length)
        {
            Debug.LogError($"[GameManager] Invalid number index {index} or numbers array not set.");
            return;
        }

        _currentPuzzle = Instantiate(letterPuzzlePrefab, letterPuzzleArea);
        _currentPuzzle.transform.localPosition = Vector3.zero;
        _currentPuzzle.transform.localRotation = Quaternion.identity;
        _currentPuzzle.transform.localScale    = Vector3.one;

        _currentPuzzle.Initialize(numbers[index], bubblePrefab);
        _currentPuzzle.OnLetterCompleted += HandleNumberCompleted;

        _currentIndex = index;

        if (uiManager != null)
            uiManager.SetActiveHighlight(index);
    }

    private void HandleNumberCompleted(LetterData completedData)
    {
        _completedFlags[_currentIndex] = true;
        SaveProgress();
        StartCoroutine(AnimateNumberAndAdvance(completedData, _currentIndex));
    }

    private IEnumerator AnimateNumberAndAdvance(LetterData data, int completedIndex)
    {
        if (!_animationInitialized && jiggleLetterObject != null)
        {
            _jiggleLetterRenderer = jiggleLetterObject.GetComponent<SpriteRenderer>();
            if (_jiggleLetterRenderer == null)
                _jiggleLetterRenderer = jiggleLetterObject.GetComponentInChildren<SpriteRenderer>();

            _jiggleParticles = jiggleLetterObject.GetComponentsInChildren<ParticleSystem>();
            _animationInitialized = true;
        }

        GameObject animObj = jiggleLetterObject;
        SpriteRenderer sr = _jiggleLetterRenderer;

        bool isFallback = false;
        if (animObj == null || sr == null)
        {
            animObj = new GameObject("JiggleNumber");
            sr = animObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1000;
            isFallback = true;
        }

        animObj.SetActive(true);
        sr.sprite = data.numberSprite;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLetterComplete();

        if (_jiggleParticles != null)
        {
            foreach (var p in _jiggleParticles)
                p.Play();
        }

        Vector3 unscaledSize = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one;
        Vector3 originalScale = Vector3.one;

        if (_currentPuzzle != null)
        {
            if (_currentPuzzle.letterBackground != null)
            {
                animObj.transform.position = _currentPuzzle.letterBackground.transform.position;
                animObj.transform.rotation = _currentPuzzle.letterBackground.transform.rotation;
                originalScale = _currentPuzzle.letterBackground.transform.lossyScale;
            }

            _currentPuzzle.OnLetterCompleted -= HandleNumberCompleted;
            Destroy(_currentPuzzle.gameObject);
            _currentPuzzle = null;
        }

        animObj.transform.localScale = originalScale;
        Quaternion originalRotation = animObj.transform.rotation;

        float waitTime = 1f;
        float tiltAmount = 15f;
        float tiltSpeed = 15f;
        float timer = 0f;

        while (timer < waitTime)
        {
            timer += Time.deltaTime;
            float angle = Mathf.Sin(Time.time * tiltSpeed) * tiltAmount;
            animObj.transform.rotation = originalRotation * Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        animObj.transform.rotation = originalRotation;

        RectTransform targetRect = uiManager != null ? uiManager.GetLetterTargetRect(completedIndex) : null;
        if (targetRect != null)
        {
            Vector3 moveStart = animObj.transform.position;
            Vector3 moveEnd = targetRect.position;

            Canvas rootCanvas = targetRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Camera.main != null)
            {
                Vector3 screenPoint = targetRect.position;
                screenPoint.z = Mathf.Abs(Camera.main.transform.position.z);
                moveEnd = Camera.main.ScreenToWorldPoint(screenPoint);
                moveEnd.z = 0f;
            }
            else
            {
                moveEnd.z = 0f;
            }

            Vector3 targetScale = originalScale;
            if (unscaledSize.x > 0.001f && unscaledSize.y > 0.001f)
            {
                Vector3 targetWorldSize = new Vector3(
                    targetRect.rect.width  * targetRect.lossyScale.x,
                    targetRect.rect.height * targetRect.lossyScale.y,
                    1f);

                if (rootCanvas != null && rootCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Camera.main != null)
                {
                    Vector3 p1 = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, Mathf.Abs(Camera.main.transform.position.z)));
                    Vector3 p2 = Camera.main.ScreenToWorldPoint(new Vector3(
                        targetRect.rect.width  * targetRect.lossyScale.x,
                        targetRect.rect.height * targetRect.lossyScale.y,
                        Mathf.Abs(Camera.main.transform.position.z)));
                    targetWorldSize = new Vector3(Mathf.Abs(p2.x - p1.x), Mathf.Abs(p2.y - p1.y), 1f);
                }

                targetScale = new Vector3(
                    targetWorldSize.x / unscaledSize.x,
                    targetWorldSize.y / unscaledSize.y,
                    originalScale.z);
            }

            float moveTime = 0f;
            float moveDuration = 1f;

            while (moveTime < moveDuration)
            {
                moveTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, moveTime / moveDuration);
                animObj.transform.position   = Vector3.Lerp(moveStart, moveEnd, t);
                animObj.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }
        }

        if (isFallback)
            Destroy(animObj);
        else
            animObj.SetActive(false);

        if (uiManager != null)
            uiManager.MarkLetterComplete(completedIndex);

        yield return new WaitForSeconds(0.2f);

        if (_currentIndex == completedIndex)
        {
            int next = (completedIndex + 1) % NUMBER_COUNT;
            _currentIndex = next;
            LoadNumber(next);
        }
    }

    private void LoadProgress()
    {
        int bitmask = PlayerPrefs.GetInt(PREFS_NUMBERS, 0);
        _completedFlags = new bool[NUMBER_COUNT];
        for (int i = 0; i < NUMBER_COUNT; i++)
            _completedFlags[i] = (bitmask & (1 << i)) != 0;

        _currentIndex = 0;
        for (int i = 0; i < NUMBER_COUNT; i++)
        {
            if (!_completedFlags[i])
            {
                _currentIndex = i;
                break;
            }
        }
    }

    private void SaveProgress()
    {
        int bitmask = 0;
        for (int i = 0; i < NUMBER_COUNT; i++)
            if (_completedFlags[i]) bitmask |= (1 << i);

        PlayerPrefs.SetInt(PREFS_NUMBERS, bitmask);
        PlayerPrefs.Save();
    }

    private void RegisterBubbleSprites()
    {
        BubbleSpriteRegistry.Clear();
        BubbleSpriteRegistry.Register(BubbleColor.Blue,   blueUnpopped,   bluePopped);
        BubbleSpriteRegistry.Register(BubbleColor.Green,  greenUnpopped,  greenPopped);
        BubbleSpriteRegistry.Register(BubbleColor.Pink,   pinkUnpopped,   pinkPopped);
        BubbleSpriteRegistry.Register(BubbleColor.Red,    redUnpopped,    redPopped);
        BubbleSpriteRegistry.Register(BubbleColor.Yellow, yellowUnpopped, yellowPopped);
    }
}
