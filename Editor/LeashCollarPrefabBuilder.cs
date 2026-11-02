// SPDX-License-Identifier: GPL-2.0-only
// Copyright (C) 2026 Nommiiee
#if UNITY_EDITOR
using System.IO;
using OpenSourceCollarAndLeash;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;
using UnityEngine;
using VRC.SDK3.Components;

namespace OpenSourceCollarAndLeash.Editor
{
    public static class LeashCollarPrefabBuilder
    {
        private const string PrefabName = "GeneratedCollarAndLeash.prefab";
        private const string RingMeshName = "ProceduralCollarRing.asset";
        private const string MaterialName = "LeashCollar_Material.mat";

        public static bool BuildPrefabAndAddToScene(Mesh customCollarMesh, Color leashColor,
            float leashLength, float leashWidth, float attachRadius, int prefabCount)
        {
            prefabCount = Mathf.Clamp(prefabCount, 1, 100);
            string builderPath = FindBuilderPath();
            if (string.IsNullOrEmpty(builderPath))
            {
                Debug.LogError("Could not find LeashCollarPrefabBuilder.cs in the imported package.");
                return false;
            }
            string normalizedBuilderPath = builderPath.Replace('\\', '/');
            int editorFolderIndex = normalizedBuilderPath.LastIndexOf("/Editor/");
            string packageRoot = normalizedBuilderPath.Substring(0, editorFolderIndex);
            string prefabDirectory = packageRoot + "/Prefabs";
            string materialDirectory = packageRoot + "/Materials";
            EnsureFolder(prefabDirectory);
            EnsureFolder(materialDirectory);

            string meshPath = materialDirectory + "/" + RingMeshName;
            string materialPath = materialDirectory + "/" + MaterialName;
            if (!EnsureProgramAssets(packageRoot)) return false;
            if (customCollarMesh != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(customCollarMesh)))
            {
                EditorUtility.DisplayDialog("Mesh must be an asset",
                    "Choose a mesh asset from the Project window. Scene-only or temporary meshes cannot be saved into the generated prefab.", "OK");
                return false;
            }
            Mesh ringMesh = customCollarMesh;
            if (ringMesh == null)
            {
                ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (ringMesh == null)
                {
                    ringMesh = CreateRingMesh();
                    AssetDatabase.CreateAsset(ringMesh, meshPath);
                }
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("No Standard or URP/Lit shader is available in this Unity project.");
                    return false;
                }
                material = new Material(shader) { name = "LeashCollar_Material" };
                material.color = new Color(0.14f, 0.16f, 0.2f, 1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string lineMaterialPath = materialDirectory + "/LeashLine_Material.mat";
            Material lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(lineMaterialPath);
            if (lineMaterial == null)
            {
                Shader lineShader = Shader.Find("Standard");
                if (lineShader == null) lineShader = Shader.Find("Universal Render Pipeline/Lit");
                if (lineShader == null)
                {
                    Debug.LogError("No Standard or URP/Lit shader is available for the leash line.");
                    return false;
                }
                lineMaterial = new Material(lineShader) { name = "LeashLine_Material", color = leashColor };
                AssetDatabase.CreateAsset(lineMaterial, lineMaterialPath);
            }
            else
            {
                lineMaterial.color = leashColor;
                EditorUtility.SetDirty(lineMaterial);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (customCollarMesh != null) ringMesh = customCollarMesh;
            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(lineMaterialPath);
            if (ringMesh == null || material == null || lineMaterial == null)
            {
                Debug.LogError("Generated collar mesh/material assets could not be reloaded. See Unity Console for import errors.");
                return false;
            }
            GameObject root = new GameObject("Open Source Collar And Leash");
            GameObject collar = CreatePickupObject(root.transform, "Collar", false, material);
            MeshFilter ringFilter = collar.GetComponent<MeshFilter>();
            ringFilter.sharedMesh = ringMesh;
            MeshRenderer ringRenderer = collar.GetComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = material;
            SphereCollider collarCollider = collar.GetComponent<SphereCollider>();
            collarCollider.radius = 0.1f;
            collarCollider.isTrigger = false;

            GameObject handle = CreatePickupObject(root.transform, "LeashHandle", true, material);
            handle.transform.localPosition = new Vector3(0.35f, 0f, 0f);
            handle.SetActive(false);

            LeashController controller = root.AddUdonSharpComponent<LeashController>();
            controller.collarPickup = collar.GetComponent<VRCPickup>();
            controller.handlePickup = handle.GetComponent<VRCPickup>();
            controller.handleObjectSync = handle.GetComponent<VRCObjectSync>();
            controller.handleEvents = handle.GetUdonSharpComponent<LeashPickupEvents>();
            controller.leashLength = Mathf.Max(0.25f, leashLength);
            controller.attachRadius = Mathf.Max(0.05f, attachRadius);

            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 17;
            line.widthMultiplier = Mathf.Max(0.001f, leashWidth);
            line.sharedMaterial = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            controller.leashRenderer = line;

            LeashPickupEvents collarEvents = collar.GetUdonSharpComponent<LeashPickupEvents>();
            collarEvents.controller = controller;
            collarEvents.isCollarPickup = true;
            LeashPickupEvents handleEvents = handle.GetUdonSharpComponent<LeashPickupEvents>();
            handleEvents.controller = controller;
            handleEvents.isCollarPickup = false;
            controller.ApplyProxyModifications();
            collarEvents.ApplyProxyModifications();
            handleEvents.ApplyProxyModifications();

            string prefabPath = prefabDirectory + "/" + PrefabName;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string expectedMeshPath = customCollarMesh == null ? meshPath : AssetDatabase.GetAssetPath(customCollarMesh);
            if (!ValidateGeneratedPrefab(prefabPath, expectedMeshPath, materialPath, lineMaterialPath)) return false;

            Vector3 sceneOrigin = Selection.activeTransform == null ? Vector3.zero : Selection.activeTransform.position;
            for (int i = 0; i < prefabCount; i++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) as GameObject;
                if (instance == null) continue;
                Undo.RegisterCreatedObjectUndo(instance, "Add VRChat Collar and Leash");
                instance.name = "Open Source Collar And Leash";
                instance.transform.position = sceneOrigin + Vector3.right * (i * 1.5f);
            }
            Debug.Log("Created and validated Open Source Collar And Leash, then added " + prefabCount + " instance(s) to the active scene.");
            return true;
        }

        private static bool ValidateGeneratedPrefab(string prefabPath, string meshPath, string materialPath, string lineMaterialPath)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("Prefab asset was not created: " + prefabPath);
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                LeashController controller = contents.GetUdonSharpComponent<LeashController>();
                if (controller == null || controller.collarPickup == null || controller.handlePickup == null ||
                    controller.handleObjectSync == null || controller.handleEvents == null || controller.leashRenderer == null)
                {
                    Debug.LogError("Generated prefab has missing LeashController references: " + prefabPath);
                    return false;
                }

                MonoBehaviour[] behaviours = contents.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] == null)
                    {
                        Debug.LogError("Generated prefab contains a missing script component: " + prefabPath);
                        return false;
                    }
                }

                UdonBehaviour[] udonBehaviours = contents.GetComponentsInChildren<UdonBehaviour>(true);
                if (udonBehaviours.Length != 3)
                {
                    Debug.LogError("Expected three UdonSharp backing behaviours; found " + udonBehaviours.Length + ". Run the builder again after UdonSharp compilation.");
                    return false;
                }
                for (int i = 0; i < udonBehaviours.Length; i++)
                {
                    if (udonBehaviours[i].programSource == null)
                    {
                        Debug.LogError("Generated UdonBehaviour has no Program Source. Recompile both UdonSharp program assets and rebuild.");
                        return false;
                    }
                }
                VRCPickup[] pickups = contents.GetComponentsInChildren<VRCPickup>(true);
                VRCObjectSync[] syncs = contents.GetComponentsInChildren<VRCObjectSync>(true);
                Transform collarForEvents = contents.transform.Find("Collar");
                Transform handleForEvents = contents.transform.Find("LeashHandle");
                LeashPickupEvents collarEvents = collarForEvents == null ? null : collarForEvents.GetUdonSharpComponent<LeashPickupEvents>();
                LeashPickupEvents handleEvents = handleForEvents == null ? null : handleForEvents.GetUdonSharpComponent<LeashPickupEvents>();
                if (pickups.Length != 2 || syncs.Length != 2 || collarEvents == null || handleEvents == null)
                {
                    Debug.LogError("Generated prefab is missing pickup, object-sync, or event components: " + prefabPath);
                    return false;
                }
                for (int i = 0; i < pickups.Length; i++)
                {
                    if (pickups[i].GetComponent<Rigidbody>() == null || pickups[i].GetComponent<Collider>() == null)
                    {
                        Debug.LogError("Generated pickup is missing a Rigidbody or Collider: " + pickups[i].name);
                        return false;
                    }
                }

                Transform collarTransform = contents.transform.Find("Collar");
                if (collarTransform == null)
                {
                    Debug.LogError("Generated prefab is missing the Collar object: " + prefabPath);
                    return false;
                }
                MeshFilter ring = collarTransform.GetComponent<MeshFilter>();
                MeshRenderer collarRenderer = collarTransform.GetComponent<MeshRenderer>();
                if (ring == null || ring.sharedMesh == null || AssetDatabase.GetAssetPath(ring.sharedMesh) != meshPath ||
                    collarRenderer == null || collarRenderer.sharedMaterial == null ||
                    AssetDatabase.GetAssetPath(collarRenderer.sharedMaterial) != materialPath ||
                    controller.leashRenderer.sharedMaterial == null ||
                    AssetDatabase.GetAssetPath(controller.leashRenderer.sharedMaterial) != lineMaterialPath)
                {
                    Debug.LogError("Generated prefab is missing its collar mesh or material assets. Keep the Materials folder with the prefab.");
                    return false;
                }
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        private static bool EnsureProgramAssets(string packageRoot)
        {
            string controllerScript = packageRoot + "/Runtime/LeashController.cs";
            string eventsScript = packageRoot + "/Runtime/LeashPickupEvents.cs";
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(controllerScript) == null ||
                AssetDatabase.LoadAssetAtPath<MonoScript>(eventsScript) == null)
            {
                Debug.LogError("UdonSharp scripts are missing or have Unity compile errors. Fix Console errors before building.");
                return false;
            }
            bool changed = false;
            changed |= EnsureProgramAsset(controllerScript, packageRoot + "/Runtime/LeashController.asset");
            changed |= EnsureProgramAsset(eventsScript, packageRoot + "/Runtime/LeashPickupEvents.asset");
            if (!changed) return true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorUtility.DisplayDialog("UdonSharp program assets created",
                "Unity created the paired UdonSharp program assets. Wait for UdonSharp compilation to finish, then click Build and Add to Scene again.", "OK");
            return false;
        }

        private static bool EnsureProgramAsset(string scriptPath, string programAssetPath)
        {
            MonoScript sourceScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (sourceScript == null)
            {
                Debug.LogError("UdonSharp C# source is missing or failed to compile: " + scriptPath);
                return false;
            }
            UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programAssetPath);
            if (programAsset != null && programAsset.sourceCsScript == sourceScript) return false;
            if (programAsset == null)
                programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            programAsset.sourceCsScript = sourceScript;
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programAssetPath) == null)
                AssetDatabase.CreateAsset(programAsset, programAssetPath);
            else
                EditorUtility.SetDirty(programAsset);
            return true;
        }

        private static GameObject CreatePickupObject(Transform parent, string objectName, bool useGravity, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = objectName;
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = objectName == "Collar" ? Vector3.one : Vector3.one * 0.12f;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            Rigidbody body = obj.AddComponent<Rigidbody>();
            body.useGravity = useGravity;
            body.isKinematic = true;
            VRCPickup pickup = obj.AddComponent<VRCPickup>();
            obj.AddComponent<VRCObjectSync>();
            obj.AddUdonSharpComponent<LeashPickupEvents>();
            return obj;
        }

        private static Mesh CreateRingMesh()
        {
            const int majorSegments = 24;
            const int minorSegments = 8;
            const float majorRadius = 0.12f;
            const float tubeRadius = 0.025f;
            Vector3[] vertices = new Vector3[majorSegments * minorSegments];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[majorSegments * minorSegments * 6];
            for (int major = 0; major < majorSegments; major++)
            {
                float a = major * Mathf.PI * 2f / majorSegments;
                for (int minor = 0; minor < minorSegments; minor++)
                {
                    float b = minor * Mathf.PI * 2f / minorSegments;
                    int index = major * minorSegments + minor;
                    vertices[index] = new Vector3(
                        (majorRadius + tubeRadius * Mathf.Cos(b)) * Mathf.Cos(a),
                        tubeRadius * Mathf.Sin(b),
                        (majorRadius + tubeRadius * Mathf.Cos(b)) * Mathf.Sin(a));
                    uv[index] = new Vector2((float)major / majorSegments, (float)minor / minorSegments);
                    int nextMajor = (major + 1) % majorSegments;
                    int nextMinor = (minor + 1) % minorSegments;
                    int first = index;
                    int second = nextMajor * minorSegments + minor;
                    int third = nextMajor * minorSegments + nextMinor;
                    int fourth = major * minorSegments + nextMinor;
                    int tri = index * 6;
                    triangles[tri] = first;
                    triangles[tri + 1] = second;
                    triangles[tri + 2] = fourth;
                    triangles[tri + 3] = second;
                    triangles[tri + 4] = third;
                    triangles[tri + 5] = fourth;
                }
            }
            Mesh mesh = new Mesh { name = "ProceduralCollarRing" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static string FindBuilderPath()
        {
            string[] matches = AssetDatabase.FindAssets("LeashCollarPrefabBuilder t:MonoScript");
            for (int i = 0; i < matches.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(matches[i]);
                if (path.EndsWith("/Editor/LeashCollarPrefabBuilder.cs")) return path;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif





















