using ShinyMinds.Missions.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static ShinyMinds.Missions.EditorTools.MissionEditorUtil;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Builds the Stranger. He is the one character the mission needs that does not
    /// exist in the scene at all — Ch29_nonPBR is imported but unused.
    /// </summary>
    public static class MissionCharacterBuilder
    {
        const string StrangerPrefabPath = PrefabRoot + "/Characters/Stranger.prefab";
        const string SourceModel = "Assets/characters/NPC_Characters/Ch29_nonPBR.fbx";

        // Teacher and Mother are both scale 2; match them so he reads as an adult
        // next to the scale-5 child.
        const float StrangerScale = 2f;

        [MenuItem("ShinyMinds/Setup/5. Build Stranger Prefab")]
        public static GameObject BuildStranger()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            if (model == null)
            {
                Debug.LogError($"No model at '{SourceModel}'.");
                return null;
            }

            var importer = AssetImporter.GetAtPath(SourceModel) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
                Debug.LogWarning("Ch29_nonPBR is not set to Humanoid yet. Run 'Setup/3. Configure Character Rigs' first, then rebuild this prefab.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                MissionAnimatorBuilder.MissionActorControllerPath);

            if (controller == null)
                Debug.LogWarning("MissionActorAnimator.controller not found. Run 'Setup/4. Build Animator Controllers' first.");

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "Stranger";
            root.transform.localScale = Vector3.one * StrangerScale;

            // Unpack so the mission components are saved into our own prefab rather
            // than fighting the imported model prefab.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            int layer = LayerMask.NameToLayer("CameraIgnore");
            if (layer >= 0) SetLayerRecursive(root, layer);

            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var mover = root.AddComponent<ActorMover>();
            Wire(mover, ("animator", animator), ("walkAnimValue", 2f), ("runAnimValue", 6f));
            if (layer >= 0) Wire(mover, ("groundMask", (LayerMask)~(1 << layer)));

            var actor = root.AddComponent<MissionActor>();
            Wire(actor, ("actorKey", "stranger"), ("animator", animator), ("mover", mover));

            GameObject prefab = SavePrefab(root, StrangerPrefabPath);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"Built {StrangerPrefabPath}. Run 'Setup/6. Wire Open Scene' to place him.", prefab);
            }

            return prefab;
        }

        /// <summary>Places an inactive Stranger under the staging root, if not already there.</summary>
        public static void EnsureStrangerInScene(Transform stagingRoot)
        {
            if (stagingRoot == null) return;

            foreach (MissionActor a in Object.FindObjectsByType<MissionActor>(FindObjectsInactive.Include))
                if (a.ActorKey == "stranger") return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StrangerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("No Stranger prefab yet — run 'Setup/5. Build Stranger Prefab'.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Stranger";
            instance.transform.SetParent(stagingRoot, false);

            Transform spawn = null;
            foreach (MissionMarker m in stagingRoot.GetComponentsInChildren<MissionMarker>(true))
                if (m.MarkerKey == "m_stranger_spawn") { spawn = m.transform; break; }

            if (spawn != null)
                instance.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            // The mission activates him in Scene 2. MissionStagingRoot is what lets an
            // inactive actor still register, since Unity skips Awake on inactive objects.
            instance.SetActive(false);

            Debug.Log("Placed an inactive Stranger under the staging root.");
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
