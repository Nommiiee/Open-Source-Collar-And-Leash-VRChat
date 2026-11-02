// SPDX-License-Identifier: GPL-2.0-only
// Copyright (C) 2026 Nommiiee
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OpenSourceCollarAndLeash.Editor
{
    public sealed class LeashCollarProjectWindow : EditorWindow
    {
        private const string ProjectTitle = "Open Source Collar And Leash";

        private Mesh collarMesh;
        private Color leashColor = new Color(0.14f, 0.16f, 0.2f, 1f);
        private float leashLength = 2.5f;
        private float leashWidth = 0.012f;
        private float attachRadius = 0.4f;
        private int prefabCount = 1;

        [MenuItem("Tools/Open Source Collar And Leash")]
        public static void Open()
        {
            LeashCollarProjectWindow window = GetWindow<LeashCollarProjectWindow>(true, ProjectTitle, true);
            window.minSize = new Vector2(390f, 440f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(ProjectTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose an optional collar mesh and leash settings. Missing values use the defaults. Building writes generated assets into this project folder and adds at least one copy to the active scene.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Collar", EditorStyles.boldLabel);
            collarMesh = (Mesh)EditorGUILayout.ObjectField("Mesh (optional)", collarMesh, typeof(Mesh), false);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Leash", EditorStyles.boldLabel);
            leashColor = EditorGUILayout.ColorField("Color", leashColor);
            leashLength = EditorGUILayout.FloatField("Length (m)", leashLength);
            leashWidth = EditorGUILayout.FloatField("Width (m)", leashWidth);
            attachRadius = EditorGUILayout.FloatField("Attach Radius (m)", attachRadius);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);
            prefabCount = EditorGUILayout.IntField("Prefabs to Add", prefabCount);
            prefabCount = Mathf.Clamp(prefabCount, 1, 100);
            EditorGUILayout.HelpBox("The default adds one prefab. Copies after the first are spaced 1.5 m apart.", MessageType.None);

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Defaults", GUILayout.Height(30f))) ResetDefaults();
                if (GUILayout.Button("Build and Add to Scene", GUILayout.Height(30f))) Build();
            }
            EditorGUILayout.Space(8f);
        }

        private void ResetDefaults()
        {
            collarMesh = null;
            leashColor = new Color(0.14f, 0.16f, 0.2f, 1f);
            leashLength = 2.5f;
            leashWidth = 0.012f;
            attachRadius = 0.4f;
            prefabCount = 1;
        }

        private void Build()
        {
            leashLength = Mathf.Max(0.25f, leashLength);
            leashWidth = Mathf.Max(0.001f, leashWidth);
            attachRadius = Mathf.Max(0.05f, attachRadius);
            prefabCount = Mathf.Clamp(prefabCount, 1, 100);
            LeashCollarPrefabBuilder.BuildPrefabAndAddToScene(
                collarMesh, leashColor, leashLength, leashWidth, attachRadius, prefabCount);
        }
    }
}
#endif
