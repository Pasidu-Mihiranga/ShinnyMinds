using System.Collections.Generic;
using System.Linq;
using ShinyMinds.Core;
using ShinyMinds.Missions.Data;
using ShinyMinds.Missions.Runtime;
using ShinyMinds.Missions.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ShinyMinds.Missions.EditorTools.MissionEditorUtil;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Builds MissionSystem.prefab and wires the open scene: player components,
    /// the staging root, and a starter set of markers placed relative to the player.
    ///
    /// Everything is idempotent — re-running reuses what already exists rather than
    /// duplicating it, so it is safe to run again after you move things around.
    /// </summary>
    public static class MissionSceneSetup
    {
        const string SystemPrefabPath = PrefabRoot + "/Missions/MissionSystem.prefab";
        const string MissionAssetPath = "Assets/GameData/Missions/Mission01_TheRoadHome.asset";

        // ------------------------------------------------------------------ 2. prefab

        [MenuItem("ShinyMinds/Setup/2. Build Mission System Prefab")]
        public static GameObject BuildSystemPrefab()
        {
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/UI/MissionUI.prefab");
            if (uiPrefab == null)
            {
                Debug.LogError("Run 'ShinyMinds/Setup/1. Build Mission UI Prefab' first.");
                return null;
            }

            var root = new GameObject("MissionSystem");

            var runner = root.AddComponent<MissionRunner>();
            var director = root.AddComponent<MissionCameraDirector>();
            root.AddComponent<CursorStateKeeper>();

            // 2D sfx source for stingers and one-shots.
            var sfxGo = new GameObject("Sfx");
            sfxGo.transform.SetParent(root.transform, false);
            var sfx = sfxGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            // Camera component disabled, and NO AudioListener: a second listener spams
            // warnings and halves the mix. Main Camera keeps the only one.
            var camGo = new GameObject("CutsceneCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            AudioListener stray = camGo.GetComponent<AudioListener>();
            if (stray != null) Object.DestroyImmediate(stray);

            var uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab);
            uiInstance.name = "MissionUI";
            uiInstance.transform.SetParent(root.transform, false);
            var view = uiInstance.GetComponent<MissionUIView>();

            MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(MissionAssetPath);
            if (mission == null)
                Debug.LogWarning($"No mission asset at {MissionAssetPath}. Run 'ShinyMinds/Build Mission 01' and assign it on the MissionRunner.");

            Wire(runner,
                ("autoStartMission", mission),
                ("autoStartDelay", 0.5f),
                ("ui", view),
                ("cameraDirector", director),
                ("sfx", sfx),
                ("typewriterCharsPerSecond", 45f),
                ("continuePromptText", "Press E"));

            // mainCamera and playerTransform are scene objects, so they are assigned on
            // the instance in step 3, not baked into the prefab.
            Wire(director,
                ("cutsceneCamera", cam),
                ("ui", view),
                ("lookAtOffset", new Vector3(0f, 1.4f, 0f)));

            GameObject prefab = SavePrefab(root, SystemPrefabPath);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                Debug.Log($"Built {SystemPrefabPath}", prefab);
            }

            return prefab;
        }

        // ------------------------------------------------------------------- 3. scene

        [MenuItem("ShinyMinds/Setup/6. Wire Open Scene")]
        public static void WireScene()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("No GameObject tagged 'Player' in the open scene. Open SampleScene first.");
                return;
            }

            int layer = EnsureCameraIgnoreLayer();

            WirePlayer(player, layer);
            EnsureMissionSystem(player);
            Transform staging = EnsureStaging(player);
            MissionCharacterBuilder.EnsureStrangerInScene(staging);
            WireOtherActors(layer);
            FixCameraCollision(layer);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Scene wired. Save the scene (Ctrl+S), then press Play.");
        }

        /// <summary>Runs the whole setup in dependency order.</summary>
        [MenuItem("ShinyMinds/Setup/Run All Steps")]
        public static void RunAll()
        {
            Mission01Builder.Build();
            MissionUIBuilder.Build();
            BuildSystemPrefab();
            MissionAnimatorBuilder.ConfigureRigs();
            MissionAnimatorBuilder.BuildControllers();
            MissionCharacterBuilder.BuildStranger();
            WireScene();

            Debug.Log("Setup complete. Save the scene (Ctrl+S), then press Play.");
        }

        // ------------------------------------------------------------------ helpers

        static int EnsureCameraIgnoreLayer()
        {
            int existing = LayerMask.NameToLayer("CameraIgnore");
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // 0-7 are reserved by Unity; MapIcon already sits at 3.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = "CameraIgnore";
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"Added layer 'CameraIgnore' at index {i}.");
                    return i;
                }
            }

            Debug.LogError("No free user layer for 'CameraIgnore'.");
            return -1;
        }

        static void WirePlayer(GameObject player, int layer)
        {
            var animator = player.GetComponent<Animator>();
            var controller = player.GetComponent<CharacterController>();

            var mover = GetOrAdd<ActorMover>(player);
            Wire(mover,
                ("animator", animator),
                ("characterController", controller),
                ("walkAnimValue", 2f),      // must match PlayerController's hardcoded values
                ("runAnimValue", 6f));

            if (layer >= 0)
                Wire(mover, ("groundMask", (LayerMask)~(1 << layer)));

            var actor = GetOrAdd<MissionActor>(player);
            Wire(actor,
                ("actorKey", "aisha"),
                ("animator", animator),
                ("mover", mover),
                ("characterController", controller));

            var binder = GetOrAdd<PlayerLockBinder>(player);
            var footsteps = player.GetComponent<footstepaudio>();

            Wire(binder,
                ("playerController", player.GetComponent<PlayerController>()),
                ("cameraController", Object.FindAnyObjectByType<CameraController>()),
                ("mapToggle", Object.FindAnyObjectByType<MapToggle>()),
                ("footsteps", footsteps),
                ("footstepSource", footsteps != null ? footsteps.audioSource : null),
                ("animator", animator));

            Debug.Log($"Wired player '{player.name}' as actor 'aisha'.");
        }

        static GameObject EnsureMissionSystem(GameObject player)
        {
            var existing = Object.FindAnyObjectByType<MissionRunner>();
            GameObject system;

            if (existing != null)
            {
                system = existing.gameObject;
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError("Run 'ShinyMinds/Setup/2. Build Mission System Prefab' first.");
                    return null;
                }

                system = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                system.name = "MissionSystem";
            }

            // Scene-only references, which cannot live in the prefab.
            var runner = system.GetComponent<MissionRunner>();
            Wire(runner, ("playerTransform", player.transform));

            var director = system.GetComponent<MissionCameraDirector>();
            Wire(director, ("mainCamera", Camera.main));

            if (Camera.main == null)
                Debug.LogWarning("No camera tagged MainCamera — assign MissionCameraDirector.mainCamera by hand.");

            return system;
        }

        static void WireOtherActors(int layer)
        {
            TryWireActor("Teacher", "teacher", layer, startInactive: false);
            TryWireActor("Mother", "mother", layer, startInactive: true);
        }

        static void TryWireActor(string objectName, string actorKey, int layer, bool startInactive)
        {
            GameObject go = FindIncludingInactive(objectName);
            if (go == null)
            {
                Debug.LogWarning($"No '{objectName}' in the scene — actor '{actorKey}' will not resolve.");
                return;
            }

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();

            // Teacher and Mother ship with no controller assigned at all.
            if (animator.runtimeAnimatorController == null)
            {
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    MissionAnimatorBuilder.MissionActorControllerPath);

                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    Debug.Log($"Assigned MissionActorAnimator to '{objectName}'.");
                }
                else
                {
                    Debug.LogWarning($"'{objectName}' has no Animator controller and none was found — run 'Setup/4. Build Animator Controllers'.");
                }
            }

            var mover = GetOrAdd<ActorMover>(go);
            Wire(mover, ("animator", animator), ("walkAnimValue", 2f), ("runAnimValue", 6f));
            if (layer >= 0) Wire(mover, ("groundMask", (LayerMask)~(1 << layer)));

            var actor = GetOrAdd<MissionActor>(go);
            Wire(actor, ("actorKey", actorKey), ("animator", animator), ("mover", mover));

            if (layer >= 0) go.layer = layer;

            if (startInactive && go.activeSelf)
            {
                go.SetActive(false);
                Debug.Log($"Set '{objectName}' inactive; the mission activates her.");
            }
        }

        static Transform EnsureStaging(GameObject player)
        {
            var existing = Object.FindAnyObjectByType<MissionStagingRoot>();
            GameObject staging;

            if (existing != null)
            {
                staging = existing.gameObject;
            }
            else
            {
                staging = new GameObject("Mission01_Staging");
                staging.AddComponent<MissionStagingRoot>();
            }

            // Mother and the Stranger live outside this hierarchy, so scan the whole
            // scene — inactive actors never run Awake and would otherwise not register.
            Wire(staging.GetComponent<MissionStagingRoot>(), ("scanWholeScene", true));

            Transform markers = staging.transform.Find("Markers");
            if (markers == null)
            {
                markers = new GameObject("Markers").transform;
                markers.SetParent(staging.transform, false);
            }

            // Placed relative to the player so they land somewhere sensible in the city.
            // Drag them into position in the Scene view — each draws a labelled gizmo.
            Vector3 p = player.transform.position;
            Vector3 fwd = player.transform.forward;
            Vector3 right = player.transform.right;

            var layout = new (string key, Vector3 pos)[]
            {
                ("m_aisha_start",          p),
                ("m_road_corner",          p + fwd * 18f),
                ("m_stranger_spawn",       p + fwd * 24f),
                ("m_stranger_call",        p + fwd * 21f),
                ("m_stranger_close",       p + fwd * 19.5f),
                ("m_stranger_flee",        p + fwd * 40f),
                ("m_aisha_stepback",       p + fwd * 16f),
                ("m_patha_exit",           p + fwd * 34f - right * 3f),
                ("m_patha_exit_stranger",  p + fwd * 34f + right * 3f),
                ("m_home_path",            p - fwd * 12f),
                ("m_home_door",            p - fwd * 20f),
                ("m_mother_door",          p - fwd * 22f),
                ("m_teacher_stand",        p + fwd * 14f + right * 10f),
                ("m_aisha_at_teacher",     p + fwd * 14f + right * 8f),
                ("m_mother_arrive_spawn",  p + fwd * 10f + right * 18f),
                ("m_mother_arrive",        p + fwd * 14f + right * 12f),

                ("m_cam_gate",             p - fwd * 6f + Vector3.up * 3f),
                ("m_cam_aisha_cu",         p + fwd * 3f + Vector3.up * 2f),
                ("m_cam_meeting",          p + fwd * 14f + right * 7f + Vector3.up * 2.5f),
                ("m_cam_close",            p + fwd * 17f + right * 5f + Vector3.up * 2f),
                ("m_cam_choice",           p + fwd * 15f + right * 6f + Vector3.up * 2.2f),
                ("m_cam_end_a",            p + fwd * 26f + right * 8f + Vector3.up * 3f),
                ("m_cam_home_door",        p - fwd * 26f + Vector3.up * 2.5f),
                ("m_cam_teacher",          p + fwd * 12f + right * 14f + Vector3.up * 2.5f),
                ("m_cam_reunion",          p + fwd * 12f + right * 16f + Vector3.up * 2.5f),
            };

            var existingKeys = new HashSet<string>(
                markers.GetComponentsInChildren<MissionMarker>(true).Select(m => m.MarkerKey));

            int created = 0;
            foreach ((string key, Vector3 pos) in layout)
            {
                if (existingKeys.Contains(key)) continue;

                // Never prefix a marker with "Waypoint": CarAI.waypoints is a serialized
                // Transform[] and the hierarchy already has ~325 objects by that name.
                var go = new GameObject(key);
                go.transform.SetParent(markers, false);
                go.transform.position = pos;
                go.transform.rotation = player.transform.rotation;

                Wire(go.AddComponent<MissionMarker>(), ("markerKey", key));
                created++;
            }

            Debug.Log($"Staging ready: {created} marker(s) created, {existingKeys.Count} already present. " +
                      "Positions are rough — drag them in the Scene view.");

            return staging.transform;
        }

        static void FixCameraCollision(int layer)
        {
            if (layer < 0) return;

            var cc = Object.FindAnyObjectByType<CameraCollision>();
            if (cc == null) return;

            // Without this the camera slams forward the moment a character stands
            // behind the player, and it already misbehaves near InteractionZone triggers.
            var so = new SerializedObject(cc);
            so.FindProperty("blockers").intValue = ~(1 << layer);
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("CameraCollision.blockers set to exclude CameraIgnore.");
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static GameObject FindIncludingInactive(string name)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name != name) continue;
                if (t.gameObject.scene.IsValid()) return t.gameObject;   // skip assets/prefabs
            }
            return null;
        }
    }
}
