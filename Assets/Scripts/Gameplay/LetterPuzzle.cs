using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages one pop-it number board: spawns bubbles, processes taps,
/// tracks completion, and fires OnNumberCompleted when all bubbles are popped.
///
/// Hierarchy expected:
///   NumberPuzzle (this script)
///     NumberBackground  (SpriteRenderer — shows the number shape)
///     BubblesContainer  (empty Transform — bubbles are spawned here)
///
/// Lifetime: instantiated by GameManager for each number; destroyed when
/// the next number loads.
/// </summary>
public class LetterPuzzle : MonoBehaviour
{
    [Tooltip("Child SpriteRenderer that displays the number shape (e.g. 0.png).")]
    public SpriteRenderer letterBackground;

    [Tooltip("Parent Transform under which bubbles are spawned at runtime.")]
    public Transform bubblesContainer;

    public LetterData Data { get; private set; }

    public event Action<LetterData> OnLetterCompleted;

    [Header("Puzzle Scale Punch")]
    [Tooltip("How much the puzzle scales down on each tap (e.g. 0.97 = 97%).")]
    public float puzzlePunchScale = 0.97f;

    [Header("Input Options")]
    [Tooltip("Radius for overlap check when popping bubbles.")]
    public float popRadius = 0.5f;

    private readonly List<Bubble> _bubbles = new List<Bubble>();
    private bool _completing;
    private Vector3 _baseScale;
    private Coroutine _puzzleAnim;

    private void OnEnable()
    {
        if (InputHandler.Instance != null)
            InputHandler.Instance.OnTap += HandleTap;
    }

    private void OnDisable()
    {
        if (InputHandler.Instance != null)
            InputHandler.Instance.OnTap -= HandleTap;
    }

    public void Initialize(LetterData numberData, Bubble bubblePrefab)
    {
        Data = numberData;
        _completing = false;
        _baseScale = transform.localScale;

        if (letterBackground == null)
        {
            Debug.LogError("[NumberPuzzle] 'letterBackground' SpriteRenderer is not assigned.");
            return;
        }
        if (numberData.numberSprite == null)
        {
            Debug.LogError($"[NumberPuzzle] NumberData '{numberData.name}' has no numberSprite.");
        }
        letterBackground.sprite = numberData.numberSprite;

        foreach (Transform child in bubblesContainer)
            Destroy(child.gameObject);
        _bubbles.Clear();

        var (unpoppedSprite, poppedSprite) = BubbleSpriteRegistry.Get(numberData.bubbleColor);
        if (unpoppedSprite == null || poppedSprite == null)
        {
            Debug.LogError($"[NumberPuzzle] Missing sprites for BubbleColor.{numberData.bubbleColor}.");
            return;
        }

        BubbleLayoutData layout = numberData.bubbleLayout;
        if (layout == null || layout.bubbles == null || layout.bubbles.Length == 0)
        {
            Debug.LogError($"[NumberPuzzle] BubbleLayoutData is null or empty for number '{numberData.number}'.");
            return;
        }

        for (int i = 0; i < layout.bubbles.Length; i++)
        {
            Bubble b = Instantiate(bubblePrefab, bubblesContainer);
            b.transform.localPosition = new Vector3(
                layout.bubbles[i].position.x,
                layout.bubbles[i].position.y,
                0f);
            b.transform.localScale = Vector3.one * layout.bubbles[i].size;

            b.unpoppedSprite = unpoppedSprite;
            b.poppedSprite   = poppedSprite;

            b.OnPopped   += OnBubblePopped;

            b.ResetBubble();

            if (b.audioSource != null && b.audioSource.clip == null && AudioManager.Instance != null)
                b.audioSource.clip = AudioManager.Instance.popClip;

            _bubbles.Add(b);
        }
    }

    private void HandleTap(Vector2 worldPos)
    {
        if (_completing) return;

        bool isMobile = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
        Bubble closestBubble = null;

        if (isMobile)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, popRadius);
            float minDistanceSq = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                Bubble bubble = hit.GetComponent<Bubble>();
                if (bubble != null && _bubbles.Contains(bubble))
                {
                    Vector2 closestPoint = hit.ClosestPoint(worldPos);
                    float distSq = (worldPos - closestPoint).sqrMagnitude;
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestBubble = bubble;
                    }
                }
            }
        }
        else
        {
            Collider2D hit = Physics2D.OverlapPoint(worldPos);
            if (hit != null)
            {
                Bubble bubble = hit.GetComponent<Bubble>();
                if (bubble != null && _bubbles.Contains(bubble))
                    closestBubble = bubble;
            }
        }

        if (closestBubble == null) return;

        if (closestBubble.TryPop())
        {
            if (_puzzleAnim != null) StopCoroutine(_puzzleAnim);
            _puzzleAnim = StartCoroutine(PuzzleScalePunch());
        }
    }

    private void OnBubblePopped(Bubble b)   => CheckCompletion();

    private void CheckCompletion()
    {
        if (_completing) return;

        foreach (Bubble b in _bubbles)
            if (!b.IsPopped) return;

        _completing = true;
        OnLetterCompleted?.Invoke(Data);
    }

    private IEnumerator PuzzleScalePunch()
    {
        Vector3 orig  = _baseScale;
        Vector3 small = orig * puzzlePunchScale;

        yield return LerpScale(orig,  small, 0.05f);
        yield return LerpScale(small, orig,  0.08f);

        transform.localScale = orig;
        _puzzleAnim = null;
    }

    private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
    }
}
