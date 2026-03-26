using UnityEngine;

public class NotActive : MonoBehaviour
{
    public GameObject objectToTurnOn;
    public float idleTime = 5f;

    private float timer = 0f;
    private bool isGameStarted = false;

    private void OnEnable()
    {
        GameManager.OnGameStartedEvent += HandleGameStarted;
        GameManager.OnReturnedToLobbyEvent += HandleReturnedToLobby;
        Bubble.OnAnyBubblePopped += HandleBubblePopped;
    }

    private void OnDisable()
    {
        GameManager.OnGameStartedEvent -= HandleGameStarted;
        GameManager.OnReturnedToLobbyEvent -= HandleReturnedToLobby;
        Bubble.OnAnyBubblePopped -= HandleBubblePopped;
    }

    private void HandleGameStarted()
    {
        isGameStarted = true;
        HandleActivity();
    }

    private void HandleBubblePopped()
    {
        HandleActivity();
    }

    private void HandleActivity()
    {
        timer = 0f;
        if (objectToTurnOn != null)
            objectToTurnOn.SetActive(false);
    }

    private void HandleReturnedToLobby()
    {
        isGameStarted = false;
        if (objectToTurnOn != null)
            objectToTurnOn.SetActive(false);
    }

    void Start()
    {
        if (objectToTurnOn != null)
            objectToTurnOn.SetActive(false);
    }

    void Update()
    {
        if (!isGameStarted) return;

        bool hasActivity = false;

        // Check for UI clicks (or touch on UI)
        var mouse = UnityEngine.InputSystem.Mouse.current;
        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverUIButton(mouse.position.ReadValue()))
                hasActivity = true;
        }
        else if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            if (IsPointerOverUIButton(touchscreen.primaryTouch.position.ReadValue()))
                hasActivity = true;
        }

        if (hasActivity)
        {
            HandleActivity();
        }

        timer += Time.deltaTime;

        // If no input for idle time -> turn on
        if (timer >= idleTime)
        {
            if (objectToTurnOn != null && !objectToTurnOn.activeSelf)
            {
                PositionOnUnpoppedBubble();
                objectToTurnOn.SetActive(true);
            }
        }
    }

    private bool IsPointerOverUIButton(Vector2 screenPosition)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        UnityEngine.EventSystems.PointerEventData eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        eventData.position = screenPosition;

        System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            // Check if what was clicked has a Button (or is a child of one, e.g. Text/Image inside Button)
            UnityEngine.UI.Button button = results[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>();
            if (button != null && button.enabled)
            {
                return true;
            }
        }
        return false;
    }



    private void PositionOnUnpoppedBubble()
    {
        Bubble[] bubbles = FindObjectsByType<Bubble>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var bubble in bubbles)
        {
            if (!bubble.IsPopped)
            {
                transform.position = bubble.transform.position;
                break;
            }
        }
    }
}
