using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time layout migration. Afterwards these are ordinary editable scene objects.
public static class CatacombsRoomLayouts
{
    public const string EmptyScene = "05-Empty-Ossuary";
    public const string GraveScene = "06-Grave-Chamber";
    private const string Art = "Assets/Catacombs/Art/";
    private const string Marker = "Room layout";
    private static readonly string[] Scenes = { "01-Pillar-Hall", "02-Ritual-Chamber", "03-Burial-Corridors", "04-Forgotten-Crossroads", EmptyScene, GraveScene };
    private static readonly string[] Titles = { "Søjlehallen", "Ritualkammeret", "Gravgangene", "Det glemte vejkryds", "Den tomme knoglesal", "Gravkammeret" };
    private static readonly Dictionary<string, string> RenamedScenes = new Dictionary<string, string>
    {
        { "01-The-Descent", "01-Pillar-Hall" }, { "02-The-Ossuary", "02-Ritual-Chamber" },
        { "03-The-Crypt", "03-Burial-Corridors" }, { "04-The-Seal", "04-Forgotten-Crossroads" },
        { "06-Empty-Ossuary", EmptyScene }, { "05-The-Warden", "07-Warden-Chamber" }
    };
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static Material material;
    private static Color Stone => new Color(0.29f, 0.34f, 0.35f);
    private static Color Edge => new Color(0.42f, 0.46f, 0.44f);
    private static Color Dark => new Color(0.12f, 0.17f, 0.18f);
    private static Color Bone => new Color(0.62f, 0.57f, 0.43f);

    [MenuItem("Catacombs/Add distinct room layouts")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing layouts.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        RenameLegacyScenes();
        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "DungeonSprites.mat");
        foreach (var name in new[] { "Square", "Circle", "Ring", "Torch", "Stone0", "Stone1", "Stone2", "Stone3" })
            sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

        // Copy the Canvas and wiring, but not an already-installed layout.
        foreach (var extra in new[] { EmptyScene, GraveScene })
        {
            string extraPath = "Assets/Scenes/" + extra + ".unity";
            if (File.Exists(extraPath))
                continue;
            var template = EditorSceneManager.OpenScene("Assets/Scenes/01-Pillar-Hall.unity");
            var previousLayout = GameObject.Find("Katakomber").transform.Find(Marker);
            if (previousLayout != null)
                UnityEngine.Object.DestroyImmediate(previousLayout.gameObject);
            EditorSceneManager.SaveScene(template, extraPath);
        }
        for (int i = 0; i < Scenes.Length; i++)
            ApplyRoom(i);
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var extra in new[] { EmptyScene, GraveScene })
        {
            string extraPath = "Assets/Scenes/" + extra + ".unity";
            int existing = buildScenes.FindIndex(scene => scene.path == extraPath);
            if (existing < 0)
                buildScenes.Add(new EditorBuildSettingsScene(extraPath, true));
            else
                buildScenes[existing] = new EditorBuildSettingsScene(extraPath, true);
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        if (File.Exists("Assets/Scenes/00-Hallway.unity"))
            EditorSceneManager.OpenScene("Assets/Scenes/00-Hallway.unity");
        Debug.Log("ROOM_LAYOUTS_OK: Six named layouts installed; existing migrated layouts are never overwritten.");
    }

    private static void RenameLegacyScenes()
    {
        foreach (var pair in RenamedScenes)
            if (File.Exists("Assets/Scenes/" + pair.Key + ".unity") && File.Exists("Assets/Scenes/" + pair.Value + ".unity"))
                throw new InvalidOperationException("Both old and renamed scenes exist: " + pair.Key + ". Resolve this before migrating.");
        foreach (var pair in RenamedScenes)
        {
            string oldPath = "Assets/Scenes/" + pair.Key + ".unity";
            if (!File.Exists(oldPath))
                continue;
            string error = AssetDatabase.MoveAsset(oldPath, "Assets/Scenes/" + pair.Value + ".unity");
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }
        var buildScenes = EditorBuildSettings.scenes;
        foreach (var scene in buildScenes)
        {
            string oldName = Path.GetFileNameWithoutExtension(scene.path);
            if (RenamedScenes.TryGetValue(oldName, out string newName))
                scene.path = "Assets/Scenes/" + newName + ".unity";
        }
        EditorBuildSettings.scenes = buildScenes;
    }

    private static void ApplyRoom(int index)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + Scenes[index] + ".unity");
        var room = UnityEngine.Object.FindAnyObjectByType<RoomController>();
        var environment = GameObject.Find("Katakomber").transform;
        if (environment.Find(Marker) != null)
            return;
        // Remove only the original four-grave layout and its central floor symbols.
        var oldNames = new HashSet<string> { "Stengrav", "Gravlåg", "Gravmærke", "Gammelt segl", "Indre segl", "Rune i gulv" };
        for (int i = environment.childCount - 1; i >= 0; i--)
            if (oldNames.Contains(environment.GetChild(i).name))
                UnityEngine.Object.DestroyImmediate(environment.GetChild(i).gameObject);
        var layout = new GameObject(Marker).transform;
        layout.SetParent(environment, false);
        room.roomTitle = Titles[index];
        room.kind = RoomKind.Combat;
        room.hud.roomText.text = "KAMMER     ·     " + Titles[index].ToUpperInvariant();
        switch (index)
        {
            case 0:
                PillarHall(layout);
                break;
            case 1:
                RitualChamber(layout);
                break;
            case 2:
                BurialCorridors(layout);
                break;
            case 3:
                Crossroads(layout);
                break;
            case 4:
                Ossuary(layout);
                break;
            case 5:
                GraveChamber(layout);
                break;
        }
        PositionActors(room, index);
        EditorSceneManager.SaveScene(scene);
    }

    private static SpriteRenderer Sprite(string name, string art, Transform parent, Vector2 position, Vector2 size, Color color, int order = 0, float angle = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        go.transform.localRotation = Quaternion.Euler(0, 0, angle);
        var visual = go.AddComponent<SpriteRenderer>();
        visual.sprite = sprites[art];
        visual.sharedMaterial = material;
        visual.color = color;
        visual.sortingOrder = order;
        return visual;
    }

    private static Transform Solid(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = LayerMask.NameToLayer("Walls");
        go.AddComponent<BoxCollider2D>().size = size;
        Sprite("Shadow", "Square", go.transform, new Vector2(0.09f, -0.1f), size + Vector2.one * 0.12f, new Color(0, 0, 0, 0.38f), 2);
        Sprite("Stone base", "Square", go.transform, Vector2.zero, size, Dark, 6);
        return go.transform;
    }

    private static void PillarHall(Transform parent)
    {
        Sprite("Worn central aisle", "Square", parent, Vector2.zero, new Vector2(2, 9.5f), new Color(0.3f, 0.34f, 0.32f, 0.13f), -4);
        foreach (var position in new[] { new Vector2(-4.4f, 1.9f), new Vector2(3.7f, 2.1f), new Vector2(-2.8f, -1.25f), new Vector2(4.8f, -1.35f) })
        {
            var pillar = Solid("Freestanding pillar", parent, position, Vector2.one * 1.35f);
            Sprite("Plinth", "Square", pillar, Vector2.zero, Vector2.one * 1.24f, Stone, 7);
            Sprite("Column", "Circle", pillar, new Vector2(0, 0.06f), Vector2.one * 1.04f, Edge, 8);
            Sprite("Column top", "Circle", pillar, new Vector2(0, 0.1f), Vector2.one * 0.78f, new Color(0.31f, 0.38f, 0.38f), 9);
            Sprite("Carved slit", "Square", pillar, new Vector2(0, 0.12f), new Vector2(0.08f, 0.42f), Dark, 9);
        }
        foreach (float x in new[] { -6.8f, 6.8f })
            Sprite("Floor inlay", "Square", parent, new Vector2(x, 0), new Vector2(0.08f, 8.6f), new Color(0.44f, 0.4f, 0.29f, 0.35f), -3);
    }

    private static void RitualChamber(Transform parent)
    {
        Color rune = new Color(0.54f, 0.26f, 0.35f, 0.75f);
        Sprite("Ritual circle", "Ring", parent, Vector2.zero, Vector2.one * 5.5f, rune, -3);
        Sprite("Inner ritual circle", "Ring", parent, Vector2.zero, Vector2.one * 4.8f, rune * 0.7f, -3);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6;
            Sprite("Ritual inscription", "Square", parent, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.55f,
                new Vector2(0.08f, 0.28f), rune, -2, angle * Mathf.Rad2Deg);
        }
        var altar = Solid("Central altar", parent, Vector2.zero, new Vector2(3.6f, 2.2f));
        Sprite("Altar rim", "Square", altar, new Vector2(0, 0.05f), new Vector2(3.4f, 2), Edge, 7);
        Sprite("Altar slab", "Square", altar, new Vector2(0, 0.08f), new Vector2(3.1f, 1.7f), Stone, 8);
        Sprite("Altar cloth", "Square", altar, new Vector2(0, 0.1f), new Vector2(0.85f, 1.9f), new Color(0.35f, 0.1f, 0.16f), 9);
        Sprite("Altar seal", "Ring", altar, new Vector2(0, 0.1f), Vector2.one * 0.65f, new Color(0.78f, 0.54f, 0.27f), 9);
        foreach (float x in new[] { -2.25f, 2.25f })
            foreach (float y in new[] { -1.65f, 1.65f })
            {
                Sprite("Candle glow", "Circle", parent, new Vector2(x, y), Vector2.one * 0.9f, new Color(1, 0.35f, 0.2f, 0.1f), 1);
                Sprite("Ritual candle", "Torch", parent, new Vector2(x, y), Vector2.one * 0.4f, new Color(1, 0.7f, 0.7f), 8);
            }
    }

    private static void BurialCorridors(Transform parent)
    {
        // Three lanes, a 1.8-unit cross-passage and broad routes around both ends.
        foreach (float x in new[] { -3f, 3f })
            foreach (float y in new[] { -2f, 2f })
            {
                var row = Solid("Burial row", parent, new Vector2(x, y), new Vector2(1.3f, 2.2f));
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 centre = new Vector2(0, i * 0.72f);
                    Sprite("Sarcophagus rim", "Square", row, centre, new Vector2(1.24f, 0.67f), Edge, 7);
                    Sprite("Sarcophagus lid", "Square", row, centre + new Vector2(0, 0.03f), new Vector2(1.05f, 0.5f), Stone, 8);
                    Sprite("Burial carving", "Square", row, centre + new Vector2(0, 0.03f), new Vector2(0.5f, 0.05f), Dark, 9);
                }
            }
        foreach (float x in new[] { -5.8f, 0, 5.8f })
            Sprite("Worn burial lane", "Square", parent, new Vector2(x, 0), new Vector2(1.3f, 8.8f), new Color(0.39f, 0.32f, 0.22f, 0.15f), -4);
    }

    private static void Crossroads(Transform parent)
    {
        Sprite("East-west road", "Square", parent, Vector2.zero, new Vector2(17.5f, 2.9f), new Color(0.26f, 0.34f, 0.32f, 0.18f), -4);
        Sprite("North-south road", "Square", parent, Vector2.zero, new Vector2(2.9f, 9.6f), new Color(0.26f, 0.34f, 0.32f, 0.18f), -4);
        BrokenWall(parent, new Vector2(-4.8f, 0), new Vector2(3.4f, 0.7f));
        BrokenWall(parent, new Vector2(4.8f, 0), new Vector2(3.4f, 0.7f));
        BrokenWall(parent, new Vector2(0, 2.7f), new Vector2(0.7f, 1.4f));
        BrokenWall(parent, new Vector2(0, -2.5f), new Vector2(0.7f, 1.2f));
        Sprite("Crossroads medallion", "Ring", parent, Vector2.zero, Vector2.one * 1.8f, new Color(0.45f, 0.44f, 0.3f, 0.55f), -3);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * 0.5f;
            Sprite("Direction mark", "Square", parent, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.6f,
                new Vector2(0.3f, 0.07f), new Color(0.55f, 0.48f, 0.3f, 0.7f), -2, i * 90);
        }
    }

    private static void BrokenWall(Transform parent, Vector2 position, Vector2 size)
    {
        var wall = Solid("Broken dividing wall", parent, position, size);
        Sprite("Wall cap", "Square", wall, Vector2.zero, size - Vector2.one * 0.08f, Stone, 7);
        bool horizontal = size.x > size.y;
        int count = horizontal ? 4 : 2;
        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) * 0.5f) * (horizontal ? 0.76f : 0.5f);
            Sprite("Old masonry", "Stone2", wall, horizontal ? new Vector2(offset, 0.04f) : new Vector2(0, offset),
                horizontal ? new Vector2(0.68f, 0.53f) : new Vector2(0.53f, 0.44f), new Color(1.6f, 1.5f, 1.3f), 8);
        }
    }

    private static void Ossuary(Transform parent)
    {
        // Bones are flat decoration only: this arena deliberately has no interior cover.
        for (int i = 0; i < 8; i++)
        {
            float x = -7.7f + i * 2.2f;
            BonePile(parent, new Vector2(x, -4.5f), i * 31);
            if (Mathf.Abs(x) > 1.5f)
                BonePile(parent, new Vector2(x, 4.5f), i * 47);
        }
        foreach (float x in new[] { -8.45f, 8.45f })
            foreach (float y in new[] { -2.5f, -0.9f, 1.2f, 2.7f })
                BonePile(parent, new Vector2(x, y), y * 36);
        Sprite("Faded floor seal", "Ring", parent, Vector2.zero, Vector2.one * 4.5f, new Color(0.42f, 0.38f, 0.28f, 0.18f), -4);
    }

    private static void BonePile(Transform parent, Vector2 position, float angle)
    {
        var pile = new GameObject("Floor bones").transform;
        pile.SetParent(parent, false);
        pile.localPosition = position;
        pile.localRotation = Quaternion.Euler(0, 0, angle);
        Sprite("Bone", "Square", pile, new Vector2(0.05f, -0.12f), new Vector2(0.62f, 0.055f), Bone, 0, 25);
        Sprite("Bone", "Square", pile, new Vector2(0.02f, -0.1f), new Vector2(0.53f, 0.065f), Bone, 0, -35);
        Sprite("Skull", "Circle", pile, new Vector2(-0.1f, 0.06f), new Vector2(0.22f, 0.26f), Bone, 1);
        foreach (float x in new[] { -0.15f, -0.06f })
            Sprite("Eye socket", "Square", pile, new Vector2(x, 0.1f), Vector2.one * 0.05f, Dark, 2);
    }

    private static void GraveChamber(Transform parent)
    {
        // Restore the original open floor, two central seals, and four corner graves.
        Sprite("Gammelt segl", "Ring", parent, Vector2.zero, Vector2.one * 3.8f, new Color(0.28f, 0.35f, 0.32f, 0.5f), -3);
        Sprite("Indre segl", "Ring", parent, Vector2.zero, Vector2.one * 3.5f, new Color(0.28f, 0.35f, 0.32f, 0.35f), -3);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4;
            Sprite("Rune i gulv", "Square", parent, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.65f,
                new Vector2(0.1f, 0.26f), new Color(0.36f, 0.43f, 0.36f, 0.7f), -2, angle * Mathf.Rad2Deg);
        }
        foreach (float x in new[] { -6.5f, 6.5f })
            foreach (float y in new[] { -2.5f, 2.5f })
            {
                var grave = Sprite("Stengrav", "Square", parent, new Vector2(x, y), new Vector2(1.15f, 1.65f), new Color(0.2f, 0.25f, 0.27f), 6);
                grave.gameObject.layer = LayerMask.NameToLayer("Walls");
                grave.gameObject.AddComponent<BoxCollider2D>();
                Sprite("Gravlåg", "Square", parent, new Vector2(x, y + 0.08f), new Vector2(0.95f, 1.4f), new Color(0.28f, 0.32f, 0.32f), 7);
                Sprite("Gravmærke", "Square", parent, new Vector2(x, y + 0.1f), new Vector2(0.1f, 0.7f), new Color(0.16f, 0.21f, 0.22f), 8);
                Sprite("Gravmærke", "Square", parent, new Vector2(x, y + 0.25f), new Vector2(0.4f, 0.1f), new Color(0.16f, 0.21f, 0.22f), 8);
            }
    }

    private static void PositionActors(RoomController room, int index)
    {
        room.hud.player.transform.position = new Vector3(0, index == 5 ? -3.7f : -3.85f, 0);
        Vector2[][] starts =
        {
            new[] { new Vector2(0, 2.9f), new Vector2(-6.5f, 2.8f), new Vector2(6.5f, 2.9f) },
            new[] { new Vector2(0, 3.4f), new Vector2(-5.5f, 2.8f), new Vector2(5.5f, 2.8f), new Vector2(-4.8f, -0.5f), new Vector2(4.8f, -0.5f) },
            new[] { new Vector2(0, 3.6f), new Vector2(-6, 2.8f), new Vector2(6, 2.8f), new Vector2(-5.5f, 0), new Vector2(5.5f, 0), new Vector2(-6, -2.3f), new Vector2(6, -2.3f) },
            new[] { new Vector2(-2, 2.8f), new Vector2(-6.5f, 2.8f), new Vector2(6.5f, 2.8f), new Vector2(-2, 0), new Vector2(2, 0), new Vector2(-7.5f, -1.4f), new Vector2(7.5f, -1.4f), new Vector2(-4, -2.5f), new Vector2(4, -2.5f) },
            new[] { new Vector2(0, 3.2f), new Vector2(-5.5f, 2.2f), new Vector2(5.5f, 2.2f), new Vector2(-5, -0.6f), new Vector2(5, -0.6f) },
            new[] { new Vector2(0, 2.7f), new Vector2(-4, 3), new Vector2(4, 3) }
        };
        if (index == 4)
        {
            foreach (var enemy in room.enemies)
                if (enemy != null)
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            string[] kinds = { "Crawler", "Shooter", "Shooter", "Crawler", "Charger" };
            room.enemies = new EnemyHealth[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Catacombs/Prefabs/" + kinds[i] + ".prefab");
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                enemy.transform.SetParent(GameObject.Find("Fjender").transform);
                var hp = enemy.GetComponent<EnemyHealth>();
                hp.room = room;
                room.enemies[i] = hp;
                var ai = enemy.GetComponent<EnemyController>();
                ai.room = room;
                ai.player = room.hud.player.GetComponent<PlayerHealth>();
            }
        }
        for (int i = 0; i < room.enemies.Length; i++)
            room.enemies[i].transform.position = starts[index][i];
    }
}
