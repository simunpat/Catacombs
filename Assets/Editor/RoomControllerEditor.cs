using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomController))]
public sealed class RoomControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        if (((RoomController)target).kind == RoomKind.Boss)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Boss testing (Editor only)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                EditorGUILayout.IntSlider(serializedObject.FindProperty("bossTestFloor"), 1, RunState.TotalFloors,
                    new GUIContent("Boss Test Floor", "Select the boss floor before pressing Play in this scene."));

            EditorGUILayout.HelpBox(
                "Open this boss scene, select a floor, then press Play. Starts with 5/5 HP and no upgrades. Normal runs ignore this setting. Stop Play Mode to change the floor.",
                MessageType.Info);

            if (GUILayout.Button("Open Testing Mode"))
                CatacombsTestingWindow.Open();

            EditorGUILayout.Space();
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "bossTestFloor");
        serializedObject.ApplyModifiedProperties();
    }
}
