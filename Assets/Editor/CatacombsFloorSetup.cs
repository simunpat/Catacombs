using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Converts authored mobs to editable spawn markers without rebuilding room geometry.
public static class CatacombsFloorSetup
{
    [MenuItem("Catacombs/Enable multiple floors")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before setup.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        foreach (var name in RunState.NormalRoomScenes)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var room = UnityEngine.Object.FindAnyObjectByType<RoomController>();
            if (room.GetComponent<RoomEncounter>() != null)
                continue;
            var encounter = room.gameObject.AddComponent<RoomEncounter>();
            encounter.crawlerPrefab = Prefab("Crawler");
            encounter.shooterPrefab = Prefab("Shooter");
            encounter.chargerPrefab = Prefab("Charger");
            encounter.enemyRoot = GameObject.Find("Fjender").transform;
            var root = new GameObject("Encounter spawn points").transform;
            root.SetParent(room.transform, false);
            Physics2D.SyncTransforms();
            var points = new List<Transform>();
            for (int x = -5; x <= 5; x++)
                for (int y = 0; y < 5; y++)
                {
                    Vector2 position = new Vector2(x * 1.5f, -2.1f + y * 1.4f);
                    if (Vector2.Distance(position, room.hud.player.transform.position) < 3.2f)
                        continue;
                    if (Physics2D.OverlapCircle(position, 0.5f, LayerMask.GetMask("Walls")) != null)
                        continue;
                    var point = new GameObject("Spawn " + (points.Count + 1)).transform;
                    point.SetParent(root, false);
                    point.position = position;
                    points.Add(point);
                }
            if (points.Count < 14)
                throw new InvalidOperationException("Insufficient spawn clearance in " + name);
            encounter.spawnPoints = points.ToArray();
            foreach (var enemy in room.enemies)
                if (enemy != null)
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            room.enemies = new EnemyHealth[0];
            EditorSceneManager.SaveScene(scene);
        }
        SetupBossDoor();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/" + RunState.HallwayScene + ".unity");
        Debug.Log("FLOOR_SETUP_OK: Six reusable encounters and boss descent door configured.");
    }

    private static EnemyHealth Prefab(string kind) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Catacombs/Prefabs/" + kind + ".prefab").GetComponent<EnemyHealth>();

    private static void SetupBossDoor()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + RunState.BossScene + ".unity");
        var room = UnityEngine.Object.FindAnyObjectByType<RoomController>();
        if (room.door.descentMarker != null)
            return;
        var stairs = new GameObject("Stairs to the next floor");
        stairs.transform.SetParent(room.door.transform, false);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Catacombs/Art/Square.png");
        var material = room.door.glow.sharedMaterial;
        for (int i = 0; i < 5; i++)
        {
            var step = new GameObject("Descending step " + (i + 1));
            step.transform.SetParent(stairs.transform, false);
            step.transform.localPosition = new Vector3(0, -0.32f + i * 0.13f, 0);
            step.transform.localScale = new Vector3(1.55f - i * 0.16f, 0.075f, 1);
            var visual = step.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.sharedMaterial = material;
            visual.sortingOrder = 8;
            visual.color = Color.Lerp(new Color(0.6f, 0.75f, 0.7f), new Color(0.16f, 0.24f, 0.23f), i / 4f);
        }
        stairs.SetActive(false);
        room.door.descentMarker = stairs;
        EditorSceneManager.SaveScene(scene);
    }
}
