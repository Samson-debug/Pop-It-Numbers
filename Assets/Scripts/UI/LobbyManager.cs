using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Lobby panel shown at game start.
///
/// The lobby presents a Play button to start the numbers game.
///
/// When the button is tapped:
///   1. LobbyPanel is hidden
///   2. GameplayPanel is shown
///   3. GameManager.StartGame() is called
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The root lobby panel GameObject. Shown on app launch.")]
    public GameObject lobbyPanel;

    [Tooltip("The root gameplay panel GameObject. Hidden on app launch.")]
    public GameObject gameplayPanel;

    [Header("Lobby Buttons")]
    [Tooltip("Button that launches the numbers game.")]
    public Button playButton;

    [Header("Gameplay Buttons")]
    [Tooltip("Button that returns to the lobby.")]
    public Button homeButton;

    private void Start()
    {
        ShowLobby();

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayPressed);

        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomePressed);
    }

    private void OnPlayPressed()
    {
        if (lobbyPanel    != null) lobbyPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(true);

        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
        else
            Debug.LogError("[LobbyManager] GameManager.Instance is null.");
    }

    private void OnHomePressed()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToLobby();
        ShowLobby();
    }

    public void ShowLobby()
    {
        if (lobbyPanel    != null) lobbyPanel.SetActive(true);
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
    }
}
