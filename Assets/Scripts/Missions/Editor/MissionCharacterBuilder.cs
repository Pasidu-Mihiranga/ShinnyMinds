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

        // ------------------------------------------------------- Teacher and Mother

        const string TeacherModel = "Assets/characters/Teacher.fbx";
        const string MotherModel = "Assets/characters/Mother.fbx";

        /// <summary>
        /// Places the Teacher and the Mother.
        ///
        /// Both are referenced all through the mission and **neither was in the scene** — only
        /// their markers were. Nothing failed loudly, because every consumer degrades: an actor
        /// key that does not resolve makes MoveActorAction a no-op, makes a Framed shot on
        /// ["aisha", "teacher"] quietly frame Aisha alone, and makes the teacher's line fall back
        /// to the subtitle bar because SpeakerBody() finds no body to hang a balloon over. Path C
        /// therefore played out with an invisible teacher.
        ///
        /// The models were imported and unused, exactly as Ch29_nonPBR was for the Stranger.
        /// MissionSceneSetup.TryWireActor finds them by name straight after this and adds the
        /// Animator, controller, ActorMover and MissionActor, so all this has to do is put the
        /// right model in the right place under the right name.
        /// </summary>
        public static void EnsureTeacherAndMother()
        {
            EnsureModelActor("Teacher", "teacher", TeacherModel, "m_teacher_stand");
            EnsureModelActor("Mother", "mother", MotherModel, "m_mother_door");
        }

        static void EnsureModelActor(string objectName, string actorKey, string modelPath,
                                     string markerKey)
        {
            foreach (MissionActor a in Object.FindObjectsByType<MissionActor>(FindObjectsInactive.Include))
                if (a.ActorKey == actorKey) return;

            // Scene ROOTS only. Scanning every Transform would also see bones and mesh nodes
            // inside the other characters, and one of those happening to be called "Mother" would
            // make this skip silently — the hardest kind of setup bug to find.
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    Debug.Log($"'{objectName}' is already in the scene; leaving it alone.", root);
                    return;
                }
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"No model at '{modelPath}', so actor '{actorKey}' cannot be placed " +
                               "and the mission will play without them.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = objectName;

            // The same scale the Stranger uses, so the adults read as adults beside the scale-5
            // child. The measured height is logged rather than trusted: if a model imports at
            // different units this is where you will see it.
            instance.transform.localScale = Vector3.one * StrangerScale;

            Transform mark = FindMarker(markerKey);

            if (mark != null)
            {
                instance.transform.SetPositionAndRotation(mark.position, mark.rotation);
            }
            else
            {
                Debug.LogWarning($"No marker '{markerKey}' in the scene, so '{objectName}' is at " +
                                 "the origin. Move them onto the street.", instance);
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Place {objectName}");

            Debug.Log($"Placed '{objectName}' as actor '{actorKey}' on '{markerKey}', " +
                      $"{MeasuredHeight(instance):0.##}m tall at scale {StrangerScale}.", instance);
        }

        static Transform FindMarker(string key)
        {
            foreach (MissionMarker m in Object.FindObjectsByType<MissionMarker>(FindObjectsInactive.Include))
                if (m.MarkerKey == key) return m.transform;

            return null;
        }

        /// <summary>Rendered height in metres, for sanity-checking an unknown model's import scale.</summary>
        static float MeasuredHeight(GameObject go)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                min = Mathf.Min(min, r.bounds.min.y);
                max = Mathf.Max(max, r.bounds.max.y);
            }

            return float.IsInfinity(min) || float.IsInfinity(max) ? 0f : max - min;
        }
    }
}
