using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Bygger almindelige, gemte Unity-objekter. Spillet bruger ikke denne fil ved runtime.
public static class CatacombsBuilder
{
    // Asset paths and room names
    private const string Root = "Assets/Catacombs";
    public static readonly string[] SceneNames =
    {
        "01-Pillar-Hall",
        "02-Ritual-Chamber",
        "03-Burial-Corridors",
        "04-Forgotten-Crossroads",
        "07-Warden-Chamber"
    };

    private static readonly string[] Titles = { "Nedstigningen", "Knoglekammeret", "Krypten", "Seglet", "Vogteren" };

    // Shared artwork and UI resources
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static Material material;
    private static Font font;

    // Prefabs and upgrades created by the builder
    private static Projectile bolt;
    private static GameObject playerPrefab;
    private static readonly GameObject[] enemyPrefabs = new GameObject[4];
    private static UpgradeDefinition[] upgrades;

    // Shared colour palette
    private static Color Ink => new Color(0.035f, 0.055f, 0.07f);
    private static Color Mint => new Color(0.48f, 0.94f, 0.79f);
    private static Color Gold => new Color(0.94f, 0.73f, 0.39f);

    [MenuItem("Catacombs/Build initial game")]
    public static void Build()
    {
        if (File.Exists("Assets/Scenes/" + SceneNames[0] + ".unity"))
            throw new InvalidOperationException("Scenerne findes allerede. Rediger dem direkte; byggeren overskriver ikke dine baner.");

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Directory.CreateDirectory(Root + "/Art");
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Upgrades");
        AssetDatabase.Refresh();

        ConfigureLayers();
        MakeArt();

        material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        AssetDatabase.CreateAsset(material, Root + "/Art/DungeonSprites.mat");
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        MakeUpgrades();
        MakePrefabs();

        for (int i = 0; i < 5; i++)
            MakeRoom(i);

        var scenes = new List<EditorBuildSettingsScene>();

        foreach (var name in SceneNames)
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/" + name + ".unity", true));

        scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/" + SceneNames[0] + ".unity");

        CatacombsRoomLayouts.Apply();
        CatacombsHallwaySetup.Apply();
        CatacombsFloorSetup.Apply();
        CatacombsTutorialSetup.Apply();
        CatacombsMenuSetup.Apply();

        Debug.Log("CATACOMBS_BUILD_OK: Hallway, six normal layouts, boss room, six upgrades and six prefabs created.");
    }

    private static void ConfigureLayers()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        string[] names = { "Player", "Enemies", "Walls" };

        for (int i = 0; i < names.Length; i++)
            layers.GetArrayElementAtIndex(8 + i).stringValue = names[i];

        tags.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MakeArt()
    {
        SaveSprite("Square", 16, 16, (x, y) => Color.white);
        SaveSprite("Circle", 32, 32, (x, y) => Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) <= 15.5f ? Color.white : Color.clear);
        SaveSprite("Ring", 32, 32, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));

            return d > 13 && d < 15.5f ? Color.white : Color.clear;
        });

        for (int i = 0; i < 4; i++)
        {
            int seed = i;
            SaveSprite("Stone" + i, 24, 24, (x, y) =>
            {
                if (x == 0 || y == 0)
                    return new Color(0.055f, 0.073f, 0.087f);

                float noise = ((x * 37 + y * 17 + seed * 11) % 13) / 700f;
                float edge = x == 1 || y == 23 ? 0.028f : 0;

                return new Color(0.105f + seed * 0.006f + noise + edge, 0.135f + noise + edge, 0.15f + noise + edge);
            });
        }

        PixelSprite("Explorer", new[] {
            "................", "......gggg......", ".....gttttg.....", "....gttttttg....",
            "....tddddddt....", "....tdwddwdt....", "....tddddddt....", ".....tggggt.....",
            "...ttttggtttt...", "...ttttggtttt...", "...ttttggtttt...", "....tttggttt....",
            "....tttttttt....", ".....dd..dd.....", ".....dd..dd.....", "................" });
        PixelSprite("Crawler", new[] {
            "................", "................", "..r........r....", "...r..rr..r.....",
            "....rrrrrr......", "..rrrddddrrr....", "....rdwwdr......", ".rrrrddddrrrr...",
            "....rrrrrr......", "..rrrddddrrr....", "....rrrrrr......", "...r..rr..r.....",
            "..r........r....", "................", "................", "................" });
        PixelSprite("Shooter", new[] {
            "................", ".....pppppp.....", "....pppppppp....", "...ppddddddpp...",
            "...pdwwddwwdp...", "...pdwwddwwdp...", "...pddddddddp...", "....pddwwddp....",
            ".....pppppp.....", "....pppggppp....", "....pppggppp....", "...pppppppppp...",
            "...pppppppppp...", "....pp.pp.pp....", "................", "................" });
        PixelSprite("Charger", new[] {
            "................", "..g..........g..", "..gg..gggg..gg..", "...gggggggggg...",
            "....gddddddg....", "...ggdwddwdgg...", "...ggddddddgg...", "....gggggggg....",
            "...ggggddgggg...", "...ggggddgggg...", "...ggggddgggg...", "....gggggggg....",
            ".....gg..gg.....", ".....dd..dd.....", "................", "................" });
        PixelSprite("Warden", new[] {
            ".g............g.", ".gg..........gg.", "..grr......rrg..", "..grrrrrrrrrrg..",
            "...rrddddddrr...", "...rdwwddwwdr...", "...rddddddddr...", "....rdwwwwdr....",
            ".rrrrrggggrrrrr.", ".rrdrrggggrrdrr.", ".rrdrrrddrrrdrr.", "...rrrrggrrrr...",
            "...rrggggggrr...", "....rrr..rrr....", "....ddd..ddd....", "................" });
        PixelSprite("Torch", new[] { "....g...", "...gg...", "..ggwg..", "..gwwg..", "...gg...", "...dd...", "...dd...", "...dd..." });
    }

    private static void PixelSprite(string name, string[] rows)
    {
        SaveSprite(name, rows[0].Length, rows.Length, (x, y) =>
        {
            switch (rows[rows.Length - 1 - y][x])
            {
                case 'g':
                    return Gold;
                case 't':
                    return new Color(0.2f, 0.72f, 0.62f);
                case 'r':
                    return new Color(0.75f, 0.27f, 0.23f);
                case 'p':
                    return new Color(0.57f, 0.39f, 0.74f);
                case 'w':
                    return new Color(1, 0.92f, 0.66f);
                case 'd':
                    return new Color(0.06f, 0.08f, 0.1f);
                default:
                    return Color.clear;
            }
        });
    }

    private static void SaveSprite(string name, int width, int height, Func<int, int, Color> pixel)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, pixel(x, y));

        texture.Apply();

        string path = Root + "/Art/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = width;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static SpriteRenderer Sprite(string name, string art, Transform parent, Vector2 position, Vector2 size, Color color, int order = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[art];
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = order;

        return renderer;
    }

    private static void MakeUpgrades()
    {
        string[] names = { "Skarpe runer", "Hurtige hænder", "Livskilde", "Lette støvler", "Splintret skud", "Skyggeskridt" };
        string[] descriptions =
        {
            "+1 samlet skade pr. salve.\nFordeles mellem projektilerne.",
            "20 % kortere tid mellem skud.\nHold museknappen nede.",
            "Gendan 2 liv, op til dit maks.\nMaksimalt liv er uændret.",
            "+12 % bevægelseshastighed.\nSkab afstand til fjenderne.",
            "+1 projektil i en vifte.\nSamme skade, delt mellem alle projektiler.",
            "20 % kortere dash-cooldown.\nDu er usårlig under dit dash."
        };

        upgrades = new UpgradeDefinition[6];

        for (int i = 0; i < 6; i++)
        {
            var u = ScriptableObject.CreateInstance<UpgradeDefinition>();
            u.title = names[i];
            u.description = descriptions[i];
            u.kind = (UpgradeKind)i;
            u.color = i % 3 == 0 ? Gold : i % 3 == 1 ? Mint : new Color(0.78f, 0.65f, 1f);
            AssetDatabase.CreateAsset(u, Root + "/Upgrades/" + ((UpgradeKind)i) + ".asset");
            upgrades[i] = u;
        }
    }

    private static Rigidbody2D Body(GameObject go, float radius)
    {
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        go.AddComponent<CircleCollider2D>().radius = radius;

        return body;
    }

    private static void MakePrefabs()
    {
        var projectile = new GameObject("Rune Bolt");
        var shot = projectile.AddComponent<Projectile>();
        shot.visual = Sprite("Light", "Circle", projectile.transform, Vector2.zero, new Vector2(0.28f, 0.13f), Mint, 20);
        bolt = PrefabUtility.SaveAsPrefabAsset(projectile, Root + "/Prefabs/RuneBolt.prefab").GetComponent<Projectile>();
        UnityEngine.Object.DestroyImmediate(projectile);

        var player = new GameObject("Player");
        player.layer = LayerMask.NameToLayer("Player");
        Body(player, 0.28f);

        var visual = Sprite("Explorer", "Explorer", player.transform, Vector2.zero, Vector2.one * 0.95f, Color.white, 10);
        Sprite("Shadow", "Circle", player.transform, new Vector2(0, -0.34f), new Vector2(0.68f, 0.24f), new Color(0, 0, 0, 0.4f), 3);

        var pivot = new GameObject("Aim").transform;
        pivot.SetParent(player.transform, false);
        Sprite("Wand", "Square", pivot, new Vector2(0.43f, 0), new Vector2(0.4f, 0.08f), Gold, 11);
        Sprite("Rune", "Circle", pivot, new Vector2(0.65f, 0), Vector2.one * 0.12f, Mint, 12);

        var movement = player.AddComponent<PlayerMovement>();
        movement.bodySprite = visual;
        movement.weapon = pivot;

        var health = player.AddComponent<PlayerHealth>();
        health.movement = movement;
        health.bodySprite = visual;

        var weapon = player.AddComponent<PlayerWeapon>();
        weapon.movement = movement;
        weapon.projectilePrefab = bolt;
        playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/Player.prefab");
        UnityEngine.Object.DestroyImmediate(player);

        string[] kinds = { "Crawler", "Shooter", "Charger", "Warden" };

        for (int i = 0; i < 4; i++)
        {
            var enemy = new GameObject(kinds[i]);
            enemy.layer = LayerMask.NameToLayer("Enemies");
            Body(enemy, i == 3 ? 0.68f : 0.3f).mass = i == 3 ? 10 : 1;

            var look = Sprite("Body", kinds[i], enemy.transform, Vector2.zero, Vector2.one * (i == 3 ? 1.9f : 1f), Color.white, 10);
            Sprite("Shadow", "Circle", enemy.transform, new Vector2(0, -0.3f),
                new Vector2(i == 3 ? 1.5f : 0.65f, 0.22f), new Color(0, 0, 0, 0.4f), 3);

            var hp = enemy.AddComponent<EnemyHealth>();
            hp.visual = look;
            hp.maxHealth = new[] { 5, 4, 18, 70 }[i];
            Sprite("Health background", "Square", enemy.transform, new Vector2(0, i == 3 ? 1.15f : 0.62f), new Vector2(0.7f, 0.06f), Ink, 14);

            var hpPivot = new GameObject("Health fill").transform;
            hpPivot.SetParent(enemy.transform, false);
            hpPivot.localPosition = new Vector3(-0.35f, i == 3 ? 1.15f : 0.62f, 0);
            Sprite("Health", "Square", hpPivot, new Vector2(0.35f, 0), new Vector2(0.7f, 0.06f), Gold, 15);
            hp.healthFill = hpPivot;

            var warning = Sprite("Attack warning", i == 2 ? "Square" : "Ring", enemy.transform,
                i == 2 ? new Vector2(1.4f, 0) : Vector2.zero,
                i == 2 ? new Vector2(2.8f, 0.16f) : Vector2.one * (i == 3 ? 2.7f : 1.4f),
                new Color(1, 0.55f, 0.25f, 0.6f), 5);
            warning.enabled = false;

            if (i == 1)
            {
                warning.name = "Charging orb";
                warning.sprite = sprites["Circle"];
                warning.transform.localPosition = new Vector3(0.55f, 0, 0);
                warning.transform.localScale = new Vector3(0.12f, 0.12f, 1);
                warning.color = new Color(1f, 0.35f, 0.2f, 0.65f);
                warning.sortingOrder = 12;
            }

            if (i == 3)
            {
                warning.name = "Charging orb";
                warning.sprite = sprites["Circle"];
                warning.transform.localScale = new Vector3(0.04f, 0.04f, 1);
                warning.color = new Color(0.8f, 0.3f, 1f, 0.35f);
                warning.sortingOrder = 12;

                var ai = enemy.AddComponent<BossController>();
                ai.health = hp;
                ai.projectilePrefab = bolt;
                ai.warning = warning;
                ai.chargeWarningTemplate = enemyPrefabs[2].GetComponent<EnemyController>().warning;
                ai.pulseMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/BossPulse.mat");
            }
            else
            {
                var ai = enemy.AddComponent<EnemyController>();
                ai.kind = (EnemyKind)i;
                ai.projectilePrefab = bolt;
                ai.warning = warning;
                ai.speed = new[] { 2f, 1.4f, 1.6f }[i];
            }

            enemyPrefabs[i] = PrefabUtility.SaveAsPrefabAsset(enemy, Root + "/Prefabs/" + kinds[i] + ".prefab");
            UnityEngine.Object.DestroyImmediate(enemy);
        }
    }

    private static void Wall(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var wall = Sprite(name, "Square", parent, position, size, new Color(0.2f, 0.25f, 0.27f), 6);
        wall.gameObject.layer = LayerMask.NameToLayer("Walls");
        wall.gameObject.AddComponent<BoxCollider2D>();
    }

    private static void MakeRoom(int index)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6.8f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        cameraObject.AddComponent<AudioListener>();

        var room = new GameObject("Spilregler").AddComponent<RoomController>();
        room.roomNumber = index + 1;
        room.roomTitle = Titles[index];
        room.upgradePool = upgrades;
        room.kind = index == 4 ? RoomKind.Boss : RoomKind.Combat;

        var environment = new GameObject("Katakomber").transform;
        var floor = new GameObject("Stengulv").transform;
        floor.SetParent(environment);

        var random = new System.Random(41 + index);

        for (int x = -9; x <= 9; x++)
            for (int y = -5; y <= 5; y++)
                Sprite("Sten " + x + "," + y, "Stone" + random.Next(4), floor, new Vector2(x, y), Vector2.one, Color.white, -5);

        Sprite("Gammelt segl", "Ring", environment, Vector2.zero, Vector2.one * 3.8f, new Color(0.28f, 0.35f, 0.32f, 0.5f), -3);
        Sprite("Indre segl", "Ring", environment, Vector2.zero, Vector2.one * 3.5f, new Color(0.28f, 0.35f, 0.32f, 0.35f), -3);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4;
            var rune = Sprite("Rune i gulv", "Square", environment,
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.65f, new Vector2(0.1f, 0.26f),
                new Color(0.36f, 0.43f, 0.36f, 0.7f), -2);
            rune.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
        }

        var walls = new GameObject("Vægge").transform;
        walls.SetParent(environment);
        Wall(walls, "Vest", new Vector2(-9.2f, 0), new Vector2(0.5f, 10.8f));
        Wall(walls, "Øst", new Vector2(9.2f, 0), new Vector2(0.5f, 10.8f));
        Wall(walls, "Syd", new Vector2(0, -5.2f), new Vector2(18.9f, 0.5f));
        Wall(walls, "Nord vest", new Vector2(-5.3f, 5.2f), new Vector2(8.3f, 0.5f));
        Wall(walls, "Nord øst", new Vector2(5.3f, 5.2f), new Vector2(8.3f, 0.5f));
        Wall(walls, "Bag døren", new Vector2(0, 5.8f), new Vector2(2.4f, 0.4f));

        for (int x = -9; x <= 9; x++)
        {
            if (Mathf.Abs(x) > 1)
                Sprite("Mursten", "Stone1", walls, new Vector2(x, 5.2f), new Vector2(0.94f, 0.5f), new Color(1.6f, 1.7f, 1.6f), 7);

            Sprite("Mursten", "Stone2", walls, new Vector2(x, -5.2f), new Vector2(0.94f, 0.5f), new Color(1.4f, 1.5f, 1.5f), 7);
        }

        // Gravene er fysiske forhindringer, som skud og dash ikke kan passere.
        // Boss-sized passages around all four graves; normal rooms keep their original layout.
        float graveX = index == 4 ? 5.8f : 6.5f;
        float graveY = index == 4 ? 2f : 2.5f;

        foreach (float x in new[] { -graveX, graveX })
            foreach (float y in new[] { -graveY, graveY })
            {
                Wall(environment, "Stengrav", new Vector2(x, y), new Vector2(1.15f, 1.65f));
                Sprite("Gravlåg", "Square", environment, new Vector2(x, y + 0.08f), new Vector2(0.95f, 1.4f), new Color(0.28f, 0.32f, 0.32f), 7);
                Sprite("Gravmærke", "Square", environment, new Vector2(x, y + 0.1f), new Vector2(0.1f, 0.7f), new Color(0.16f, 0.21f, 0.22f), 8);
                Sprite("Gravmærke", "Square", environment, new Vector2(x, y + 0.25f), new Vector2(0.4f, 0.1f), new Color(0.16f, 0.21f, 0.22f), 8);
            }

        foreach (float x in new[] { -8.6f, 8.6f })
            foreach (float y in new[] { -3.7f, 0f, 3.7f })
            {
                Sprite("Fakkellys", "Circle", environment, new Vector2(x, y), Vector2.one * 2.2f, new Color(1f, 0.55f, 0.2f, 0.04f), 1);
                Sprite("Fakkellys kerne", "Circle", environment, new Vector2(x, y), Vector2.one * 1.2f, new Color(1f, 0.55f, 0.2f, 0.07f), 2);
                Sprite("Fakkel", "Torch", environment, new Vector2(x, y), Vector2.one * 0.65f, Color.white, 8);
            }

        var doorObject = new GameObject("Udgang");
        doorObject.transform.position = new Vector3(0, 4.95f, 0);

        var door = doorObject.AddComponent<RoomDoor>();
        door.room = room;
        room.door = door;

        var trigger = doorObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.9f, 1.1f);
        door.glow = Sprite("Lys bag døren", "Square", door.transform, Vector2.zero, new Vector2(2, 1.1f), new Color(0.07f, 0.12f, 0.13f), 1);

        var bars = new GameObject("Låst gitter");
        bars.transform.SetParent(door.transform, false);
        bars.layer = LayerMask.NameToLayer("Walls");
        bars.AddComponent<BoxCollider2D>().size = new Vector2(2, 0.25f);

        for (int i = -3; i <= 3; i++)
            Sprite("Jernstang", "Square", bars.transform, new Vector2(i * 0.26f, 0), new Vector2(0.08f, 0.9f), Gold * 0.65f, 8);

        door.bars = bars;

        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = new Vector3(0, -3.7f, 0);

        var movement = player.GetComponent<PlayerMovement>();
        movement.room = room;

        var playerHealth = player.GetComponent<PlayerHealth>();
        playerHealth.room = room;
        player.GetComponent<PlayerWeapon>().room = room;

        var enemyRoot = new GameObject("Fjender").transform;
        int[][] groups =
        {
            new[] { 0, 0, 0 },
            new[] { 0, 1, 0, 1, 0 },
            new[] { 0, 1, 2, 0, 1, 2, 0 },
            new[] { 2, 1, 0, 2, 1, 0, 1, 0, 2 },
            new[] { 3 }
        };

        Vector2[] positions =
        {
            new Vector2(0, 2.7f),
            new Vector2(-4, 3),
            new Vector2(4, 3),
            new Vector2(-3, 0),
            new Vector2(3, 0),
            new Vector2(-7.9f, 1),
            new Vector2(7.9f, 1),
            new Vector2(-4.4f, -2),
            new Vector2(4.4f, -2)
        };
        var healths = new List<EnemyHealth>();

        for (int i = 0; i < groups[index].Length; i++)
        {
            int kind = groups[index][i];
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefabs[kind]);
            enemy.transform.SetParent(enemyRoot);
            enemy.transform.position = positions[i];

            var hp = enemy.GetComponent<EnemyHealth>();
            hp.room = room;
            healths.Add(hp);

            if (kind == 3)
            {
                var ai = enemy.GetComponent<BossController>();
                ai.room = room;
                ai.player = playerHealth;
            }
            else
            {
                var ai = enemy.GetComponent<EnemyController>();
                ai.room = room;
                ai.player = playerHealth;
            }
        }

        room.enemies = healths.ToArray();
        MakeHUD(room, movement, index == 4 ? healths[0] : null);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + SceneNames[index] + ".unity");
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return rect;
    }

    private static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var rect = Rect(name, parent, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        return image;
    }

    private static Text Label(string name, Transform parent, string content, Vector2 anchor,


        Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var rect = Rect(name, parent, anchor, position, size);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;

        return text;
    }

    private static GameObject Overlay(string name, Transform parent)
    {
        var panel = Panel(name, parent, Vector2.one * 0.5f, Vector2.zero, new Vector2(1280, 800), new Color(0.018f, 0.028f, 0.038f, 0.95f));
        panel.rectTransform.anchorMin = Vector2.zero;
        panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.sizeDelta = Vector2.zero;

        return panel.gameObject;
    }

    private static void MakeHUD(RoomController room, PlayerMovement player, EnemyHealth boss)
    {
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        scaler.matchWidthOrHeight = 0.5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var hud = canvasObject.AddComponent<GameHUD>();
        hud.room = room;
        hud.player = player;
        hud.boss = boss;
        room.hud = hud;

        Transform root = canvasObject.transform;
        var top = Panel("Top", root, new Vector2(0.5f, 1), new Vector2(0, -43), new Vector2(1280, 86), Ink);
        top.rectTransform.anchorMin = new Vector2(0, 1);
        top.rectTransform.anchorMax = Vector2.one;
        top.rectTransform.sizeDelta = new Vector2(0, 86);
        Label("Title", root, "C A T A C O M B S", new Vector2(0, 1), new Vector2(200, -26), new Vector2(340, 36), 32, Gold);
        hud.roomText = Label("Room", root, "", new Vector2(0, 1), new Vector2(300, -59), new Vector2(540, 26), 18, new Color(0.65f, 0.72f, 0.72f));
        hud.healthText = Label("HP", root, "", new Vector2(1, 1), new Vector2(-134, -27), new Vector2(210, 32), 23, Mint, TextAnchor.MiddleRight);
        hud.objectiveText = Label("Objective", root, "", new Vector2(1, 1), new Vector2(-315, -59), new Vector2(570, 26), 18, Gold, TextAnchor.MiddleRight);

        var bottom = Panel("Bottom", root, new Vector2(0.5f, 0), new Vector2(0, 43), new Vector2(1280, 86), Ink);
        bottom.rectTransform.anchorMin = Vector2.zero;
        bottom.rectTransform.anchorMax = new Vector2(1, 0);
        bottom.rectTransform.sizeDelta = new Vector2(0, 86);
        hud.buildText = Label("Build", root, "", new Vector2(0, 0), new Vector2(450, 62), new Vector2(840, 25), 18, Gold);
        hud.statsText = Label("Stats", root, "", new Vector2(0, 0), new Vector2(320, 39), new Vector2(580, 22), 18, new Color(0.55f, 0.63f, 0.64f));
        Label("Controls", root,
            "WASD  BEVÆG     MUS  SIGT / SKYD     SPACE  DASH     R  NYT RUN     ESC  PAUSE", new Vector2(0, 0),
            new Vector2(470, 16), new Vector2(880, 22), 18, new Color(0.65f, 0.72f, 0.72f));
        hud.dashText = Label("Dash label", root, "", new Vector2(1, 0), new Vector2(-148, 55), new Vector2(230, 28), 18, Mint, TextAnchor.MiddleRight);
        Panel("Dash track", root, new Vector2(1, 0), new Vector2(-147, 27), new Vector2(220, 5), new Color(0.15f, 0.23f, 0.24f));
        hud.dashFill = Panel("Dash", root, new Vector2(1, 0), new Vector2(-147, 27), new Vector2(220, 5), Mint);
        hud.dashFill.sprite = sprites["Square"];
        hud.dashFill.type = Image.Type.Filled;
        hud.dashFill.fillMethod = Image.FillMethod.Horizontal;

        hud.bossPanel = Rect("Boss HP", root, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(460, 45)).gameObject;
        Label("Boss name", hud.bossPanel.transform, "KATAKOMBERNES VOGTER", Vector2.one * 0.5f,
            new Vector2(0, 13), new Vector2(460, 22), 18, Gold, TextAnchor.MiddleCenter);
        Panel("Boss track", hud.bossPanel.transform, Vector2.one * 0.5f, new Vector2(0, -8), new Vector2(460, 7), Ink);
        hud.bossFill = Panel("Boss fill", hud.bossPanel.transform, Vector2.one * 0.5f, new Vector2(0, -8),
            new Vector2(460, 7), new Color(0.85f, 0.3f, 0.25f));
        hud.bossFill.sprite = sprites["Square"];
        hud.bossFill.type = Image.Type.Filled;
        hud.bossFill.fillMethod = Image.FillMethod.Horizontal;

        hud.upgradePanel = Overlay("Vælg opgradering", root);

        Transform choices = hud.upgradePanel.transform;
        Label("Eyebrow", choices, "R U M M E T   E R   R Y D D E T", Vector2.one * 0.5f,
            new Vector2(0, 205), new Vector2(900, 30), 18, Gold, TextAnchor.MiddleCenter);
        Label("Heading", choices, "Vælg din næste styrke", Vector2.one * 0.5f,
            new Vector2(0, 155), new Vector2(1000, 64), 38, Color.white, TextAnchor.MiddleCenter);
        Label("Subheading", choices, "Vælg heling eller en permanent opgradering.",
            Vector2.one * 0.5f, new Vector2(0, 104), new Vector2(900, 35), 18, new Color(0.6f, 0.7f, 0.7f),
            TextAnchor.MiddleCenter);
        hud.choiceButtons = new Button[3];
        hud.choiceTitles = new Text[3];
        hud.choiceDescriptions = new Text[3];
        hud.choiceNumbers = new Text[3];

        for (int i = 0; i < 3; i++)
        {
            var card = Panel("Valg " + (i + 1), choices, Vector2.one * 0.5f, new Vector2((i - 1) * 322, -62),
                new Vector2(304, 236), new Color(0.085f, 0.125f, 0.145f));
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;

            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.5f, 1.5f);
            colors.pressedColor = new Color(0.6f, 0.9f, 0.8f);
            button.colors = colors;
            hud.choiceButtons[i] = button;
            hud.choiceNumbers[i] = Label("Key", card.transform, "0" + (i + 1), Vector2.one * 0.5f, new Vector2(0, 74), new Vector2(254, 38), 28, Gold);
            hud.choiceTitles[i] = Label("Upgrade", card.transform, "", Vector2.one * 0.5f, new Vector2(0, 22), new Vector2(254, 45), 22, Mint);
            hud.choiceDescriptions[i] = Label("Description", card.transform, "", Vector2.one * 0.5f, new Vector2(0, -47),
                new Vector2(254, 92), 18, new Color(0.75f, 0.8f, 0.8f));
        }

        Label("Pick hint", choices, "KLIK PÅ ET KORT ELLER TRYK 1, 2 ELLER 3",
            Vector2.one * 0.5f, new Vector2(0, -225), new Vector2(900, 30), 18, Gold, TextAnchor.MiddleCenter);
        hud.upgradePanel.SetActive(false);

        hud.resultPanel = Overlay("Run slut", root);
        Label("End eyebrow", hud.resultPanel.transform, "C A T A C O M B S",
            Vector2.one * 0.5f, new Vector2(0, 145), new Vector2(900, 40), 18, Gold, TextAnchor.MiddleCenter);
        hud.resultTitle = Label("End title", hud.resultPanel.transform, "", Vector2.one * 0.5f,
            new Vector2(0, 69), new Vector2(1100, 85), 44, Mint, TextAnchor.MiddleCenter);
        hud.resultBody = Label("End message", hud.resultPanel.transform, "", Vector2.one * 0.5f,
            new Vector2(0, -15), new Vector2(900, 80), 19, new Color(0.7f, 0.78f, 0.78f), TextAnchor.MiddleCenter);

        var restart = Panel("Nyt run", hud.resultPanel.transform, Vector2.one * 0.5f, new Vector2(0, -127),
            new Vector2(280, 58), new Color(0.18f, 0.34f, 0.31f));
        hud.restartButton = restart.gameObject.AddComponent<Button>();
        hud.restartButton.targetGraphic = restart;
        Label("Button label", restart.transform, "PRØV IGEN   [R]", Vector2.one * 0.5f,
            Vector2.zero, new Vector2(260, 50), 18, Mint, TextAnchor.MiddleCenter);
        hud.resultPanel.SetActive(false);
        hud.pausePanel = Overlay("Pause", root);
        Label("Pause title", hud.pausePanel.transform, "PAUSE", Vector2.one * 0.5f,
            new Vector2(0, 35), new Vector2(600, 80), 48, Gold, TextAnchor.MiddleCenter);
        Label("Pause hint", hud.pausePanel.transform, "ESC  FORTSÆT     ·     R  NYT RUN",
            Vector2.one * 0.5f, new Vector2(0, -40), new Vector2(650, 50), 18, Mint, TextAnchor.MiddleCenter);
        hud.pausePanel.SetActive(false);
    }
}
