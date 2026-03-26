using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all UI elements:
///   - Bottom number tray (10 LetterButtonUI instances, built at runtime)
///   - Green checkmarks for completed numbers
///
/// Attach to a persistent GameObject in the scene and wire all references
/// in the Inspector before pressing Play.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Bottom Tray")]
    [Tooltip("The content RectTransform inside the ScrollRect. Has a HorizontalLayoutGroup.")]
    public RectTransform alphabetTrayContent;

    [Tooltip("Prefab for each number button (has LetterButtonUI component).")]
    public GameObject letterButtonPrefab;

    [Header("Number Sprites for Tray")]
    [Tooltip("10 number sprites in order 0-9.")]
    public Sprite[] numberSprites;

    private const int NUMBER_COUNT = 10;
    private LetterButtonUI[] _numberButtons = new LetterButtonUI[NUMBER_COUNT];

    private void Awake()
    {
        BuildTray();
    }

    public void SetActiveHighlight(int index)
    {
        for (int i = 0; i < NUMBER_COUNT; i++)
        {
            if (_numberButtons[i] != null)
                _numberButtons[i].SetHighlight(i == index);
        }
    }

    public void MarkLetterComplete(int index)
    {
        if (index >= 0 && index < NUMBER_COUNT && _numberButtons[index] != null)
            _numberButtons[index].ShowCheckmark(true);
    }

    public RectTransform GetLetterTargetRect(int index)
    {
        if (index >= 0 && index < NUMBER_COUNT && _numberButtons[index] != null)
            return _numberButtons[index].letterImage.rectTransform;
        return null;
    }

    public void RefreshAll(bool[] completedFlags, int activeIndex)
    {
        for (int i = 0; i < NUMBER_COUNT; i++)
        {
            if (_numberButtons[i] == null) continue;
            _numberButtons[i].ShowCheckmark(completedFlags != null && i < completedFlags.Length && completedFlags[i]);
            _numberButtons[i].SetHighlight(i == activeIndex);
        }
    }

    private void BuildTray()
    {
        if (alphabetTrayContent == null || letterButtonPrefab == null)
        {
            Debug.LogError("[UIManager] alphabetTrayContent or letterButtonPrefab is not assigned.");
            return;
        }

        foreach (Transform child in alphabetTrayContent)
            Destroy(child.gameObject);

        for (int i = 0; i < NUMBER_COUNT; i++)
        {
            int capturedIndex = i;

            GameObject go = Instantiate(letterButtonPrefab, alphabetTrayContent);
            LetterButtonUI lb = go.GetComponent<LetterButtonUI>();

            if (lb == null)
            {
                Debug.LogError("[UIManager] letterButtonPrefab is missing LetterButtonUI component.");
                continue;
            }

            Sprite sprite = (numberSprites != null && i < numberSprites.Length) ? numberSprites[i] : null;
            lb.Setup(sprite);
            lb.button.onClick.AddListener(() => GameManager.Instance.SelectNumber(capturedIndex));

            _numberButtons[i] = lb;
        }
    }
}
