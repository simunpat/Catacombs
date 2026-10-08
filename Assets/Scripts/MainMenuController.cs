using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    // Menu buttons
    public Button startButton;
    public Button tutorialButton;
    public Button quitButton;

    // Prevent repeated actions while leaving the menu
    private bool loading;

    private void Awake()
    {
        Time.timeScale = 1;
        RunState.Reset();
        TutorialProgress.Replaying = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        startButton.onClick.AddListener(() => StartGame(false));
        tutorialButton.onClick.AddListener(() => StartGame(true));
        quitButton.onClick.AddListener(Quit);
    }

    private void Start()
    {
        GameSfx.StopForMenu();
        GameMusic.Follow(null); // Infinite Darkness, with the existing crossfade.

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    public void StartGame(bool replayTutorial)
    {
        if (loading)
            return;

        loading = true;
        startButton.interactable = tutorialButton.interactable = quitButton.interactable = false;

        Time.timeScale = 1;
        RunState.Reset();
        TutorialProgress.Replaying = replayTutorial;

        SceneManager.LoadScene(TutorialProgress.ShouldPlay ? RunState.TutorialStartScene : RunState.HallwayScene);
    }

    public void Quit()
    {
        if (loading)
            return;

        loading = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
