using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Creates a regular, editable scene. Existing menu artwork is never overwritten.
public static class CatacombsMenuSetup
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    // Menu colour palette
    private static Color Gold => new Color(.94f, .73f, .39f);
    private static Color Mint => new Color(.6f, 1f, .87f);

    [MenuItem("Catacombs/Set up main menu")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!File.Exists(ScenePath))
            CreateScene();

        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("MAIN_MENU_SETUP_OK: MainMenu is the first build scene.");
    }

    private static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";

        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .035f, .045f);

        var canvasObject = new GameObject("Main menu canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        scaler.matchWidthOrHeight = .5f;

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var menu = new GameObject("Main menu").AddComponent<MainMenuController>();
        var root = canvasObject.transform;
        var background = Panel("Background", root, Vector2.zero, Vector2.zero, new Color(.025f, .035f, .045f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.sizeDelta = Vector2.zero;

        foreach (int side in new[] { -1, 1 })
        {
            for (int y = -4; y <= 4; y++)
            {
                var stone = Panel("Stone pillar", root, new Vector2(side * 460, y * 84), new Vector2(112, 80), new Color(.8f, .87f, .85f));
                stone.sprite = Art("Stone" + ((y + 4) % 4));
            }

            Panel("Pillar capital", root, new Vector2(side * 460, 356), new Vector2(144, 18), new Color(.19f, .24f, .25f));
            Panel("Pillar base", root, new Vector2(side * 460, -356), new Vector2(144, 18), new Color(.19f, .24f, .25f));

            var glow = Panel("Torch glow", root, new Vector2(side * 460, 45), new Vector2(190, 190), new Color(1f, .5f, .15f, .09f));
            glow.sprite = Art("Circle");

            var torch = Panel("Torch", root, new Vector2(side * 460, 45), new Vector2(56, 56), Color.white);
            torch.sprite = Art("Torch");
            torch.preserveAspect = true;
        }

        var sigil = Panel("Faded seal", root, new Vector2(0, 140), new Vector2(330, 330), new Color(.35f, .44f, .37f, .12f));
        sigil.sprite = Art("Ring");
        Label("Title", root, "C A T A C O M B S", new Vector2(0, 155), new Vector2(900, 100), 60, Gold);
        Panel("Divider", root, new Vector2(0, 25), new Vector2(330, 2), new Color(.44f, .36f, .23f));
        menu.startButton = Button("Start game", root, "START SPIL", -55, true);
        menu.tutorialButton = Button("Play introduction", root, "SPIL INTRO", -135, false);
        menu.quitButton = Button("Quit", root, "AFSLUT", -215, false);

        // Explicit navigation prevents decorative objects or later additions from
        // changing the simple, wrapping keyboard/controller menu order.
        Button[] buttons = { menu.startButton, menu.tutorialButton, menu.quitButton };

        for (int i = 0; i < buttons.Length; i++)
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + 2) % 3],
                selectOnDown = buttons[(i + 1) % 3]
            };

        events.GetComponent<EventSystem>().firstSelectedGameObject = menu.startButton.gameObject;
        Label("Footer", root, "MUS ELLER PILETASTER  ·  ENTER VÆLGER", new Vector2(0, -321),
            new Vector2(850, 36), 18, new Color(.48f, .58f, .58f));
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Catacombs/Art/" + name + ".png");

    private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var image = go.GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static Text Label(string name, Transform parent, string content, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var label = go.GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = size;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.text = content;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;

        return label;
    }

    private static Button Button(string name, Transform parent, string label, float y, bool primary)
    {
        var panel = Panel(name, parent, new Vector2(0, y), new Vector2(330, 60), primary ? new Color(.17f, .33f, .29f) : new Color(.09f, .14f, .15f));
        panel.raycastTarget = true;

        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;

        var colors = button.colors;
        colors.highlightedColor = colors.selectedColor = new Color(1.45f, 1.45f, 1.3f);
        colors.pressedColor = new Color(.7f, .9f, .8f);
        button.colors = colors;
        Label("Label", panel.transform, label, Vector2.zero, new Vector2(310, 54), 22, primary ? Mint : Gold);

        return button;
    }
}
