using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CatacombsPauseMenuSetup
{
    [MenuItem("Catacombs/Set up pause menu buttons")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var original = EditorSceneManager.GetSceneManagerSetup();
        var scenes = new List<string> { RunState.HallwayScene, RunState.TutorialStartScene, RunState.TutorialCombatScene, RunState.BossScene };
        scenes.AddRange(RunState.NormalRoomScenes);

        try
        {
            foreach (string name in scenes)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var hud = Object.FindAnyObjectByType<GameHUD>();

                if (hud == null)
                    continue;

                Configure(hud);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            // Batch mode can start in an untitled scene, which Unity cannot
            // restore through SceneManagerSetup because it has no asset path.
            if (original.Length > 0 && System.Array.TrueForAll(original, s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(original);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        Debug.Log("PAUSE_MENU_SETUP_OK: restart, main menu and quit added to all gameplay scenes.");
    }

    public static void Configure(GameHUD hud)
    {
        Transform panel = hud.pausePanel.transform;
        var oldReplayButton = panel.Find("Replay introduction");

        if (oldReplayButton != null)
            Object.DestroyImmediate(oldReplayButton.gameObject);

        SetPosition(panel.Find("Pause title") as RectTransform, 225);

        var hint = panel.Find("Pause hint").GetComponent<Text>();
        hint.rectTransform.sizeDelta = new Vector2(950, 100);
        SetPosition(hint.rectTransform, 132);
        hud.pauseRestartButton = ConfigureButton(hud, hud.pauseRestartButton, "Restart run", "Start igen", 30);
        hud.pauseMenuButton = ConfigureButton(hud, hud.pauseMenuButton, "Return to main menu", "Tilbage til start menu", -45);
        hud.pauseQuitButton = ConfigureButton(hud, hud.pauseQuitButton, "Quit game", "Afslut", -120);

        var buttons = new List<Button> { hud.pauseRestartButton, hud.pauseMenuButton, hud.pauseQuitButton };

        for (int i = 0; i < buttons.Count; i++)
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count],
                selectOnDown = buttons[(i + 1) % buttons.Count]
            };

        EditorUtility.SetDirty(hud);
    }

    private static Button ConfigureButton(GameHUD hud, Button button, string name, string title, float y)
    {
        if (button == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(hud.pausePanel.transform, false);
            button = go.GetComponent<Button>();

            var background = go.GetComponent<Image>();
            background.color = new Color(.12f, .23f, .24f, .98f);
            button.targetGraphic = background;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(go.transform, false);
        }

        var rect = button.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(440, 58);
        SetPosition(rect, y);

        var label = button.GetComponentInChildren<Text>(true);
        label.font = hud.resultTitle.font;
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(.75f, .96f, .88f);
        label.raycastTarget = false;
        label.text = title;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(10, 2);
        label.rectTransform.offsetMax = new Vector2(-10, -2);

        var colors = button.colors;
        colors.highlightedColor = colors.selectedColor = new Color(1.4f, 1.5f, 1.4f);
        colors.pressedColor = new Color(.6f, .9f, .8f);
        button.colors = colors;

        return button;
    }

    private static void SetPosition(RectTransform rect, float y)
    {
        if (rect != null)
            rect.anchoredPosition = new Vector2(0, y);
    }
}
