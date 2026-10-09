using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CatacombsSfxSetup
{
    // Keep this order aligned with SoundCue; each index selects a specific sound.
    public static readonly string[] Files =
    {
        "sfx_wpn_laser7",
        "sfx_wpn_laser9",
        "sfx_deathscream_alien1",
        "sfx_deathscream_human3",
        "sfx_deathscream_human1",
        "sfx_sounds_fanfare3",
        "sfx_sounds_impact3",
        "sfx_sounds_impact7",
        "sfx_movement_dooropen4",
        "sfx_wpn_cannon1",
        "sfx_wpn_sword3",
        "sfx_sounds_powerup16",
        "sfx_sounds_powerup4",
        "sfx_sounds_damage3",
        "sfx_sound_mechanicalnoise1",
        "sfx_vehicle_plainloop",
        "sfx_sound_depressurizing",
        "sfx_exp_long4"
    };

    [MenuItem("Catacombs/Set up sound effects")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");

        const string folder = "Assets/Catacombs/Resources";
        const string path = folder + "/CatacombsSfx.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("Sound effects are already set up. Edit CatacombsSfx to change the sounds.");
            return;
        }

        var clips = new AudioClip[Files.Length];

        for (int i = 0; i < clips.Length; i++)
            clips[i] = Clip(Files[i]);

        var steps = Clip("sfx_movement_footstepsloop4_fast");

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Catacombs", "Resources");

        var go = new GameObject("Catacombs Sound Effects");

        try
        {
            var sfx = go.AddComponent<GameSfx>();
            sfx.clips = clips;
            sfx.footsteps = steps;
            sfx.footstepSource = Source(go, true);

            sfx.voices = new AudioSource[16];

            for (int i = 0; i < sfx.voices.Length; i++)
                sfx.voices[i] = Source(go, false);

            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("SFX_SETUP_OK: all requested clips assigned.");
    }

    private static AudioSource Source(GameObject go, bool loop)
    {
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.loop = loop;

        return source;
    }

    private static AudioClip Clip(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip", new[] { "Assets/Catacombs/Audio/SFX" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Path.GetFileName(path) == name + ".wav")
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        throw new InvalidOperationException("Missing sound: " + name + ".wav");
    }
}
