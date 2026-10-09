using System;
using UnityEditor;
using UnityEngine;

public static class CatacombsMusicSetup
{
    // Prefab destination and source music folder
    private const string Folder = "Assets/Catacombs/Resources";
    private const string MusicFolder = "Assets/Catacombs/Audio/Music/18 High Quality 8-bit Musics/";

    [MenuItem("Catacombs/Set up music")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before setting up music.");

        string path = Folder + "/CatacombsMusic.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("Music is already configured. Edit the CatacombsMusic prefab to change it.");
            return;
        }

        var exploration = Track("18. Infinite Darkness.mp3");
        var combat = Track("07. MonsterVania #1.mp3");
        var boss = Track("12. Strong Boss.mp3");

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Catacombs", "Resources");

        var go = new GameObject("Catacombs Music");

        try
        {
            var music = go.AddComponent<GameMusic>();
            music.exploration = exploration;
            music.combat = combat;
            music.boss = boss;

            music.firstSource = Source(go);
            music.secondSource = Source(go);

            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("MUSIC_SETUP_OK: Infinite Darkness / MonsterVania #1 / Strong Boss.");
    }

    private static AudioSource Source(GameObject go)
    {
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0;
        source.volume = 0;
        source.priority = 0;

        return source;
    }

    private static AudioClip Track(string name)
    {
        string path = MusicFolder + name;
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;

        if (importer == null)
            throw new InvalidOperationException("Missing music: " + path);

        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
