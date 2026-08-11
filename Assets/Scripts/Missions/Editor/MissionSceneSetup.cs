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

        /// <summary>
        /// Printed by every menu item here. Unity runs a menu item against the assembly it has
        /// already compiled, so clicking one before a recompile finishes silently runs the
        /// PREVIOUS version — which looks exactly like the change not working. If the Console does
        /// not name the change you just made, give the editor focus, wait for the spinner, and run
        /// it again.
        /// </summary>
        const string SetupStamp = "open-world mission offer, no auto-start, Teacher + Mother placed";

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
                // Null on purpose — see EnsureMissionSystem. The school zone offers the mission;
                // nothing starts on load. The asset is still loaded above so a warning fires if
                // it is missing.
                ("autoStartMission", null),
                ("autoStartDelay", 0.5f),
                ("ui", view),
                ("cameraDirector", director),
                ("sfx", sfx),
                ("typewriterCharsPerSecond", 45f),
                ("continuePromptText", "Press E"),
                ("touchContinuePromptText", "Touch"));

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
            // Before WireOtherActors, which finds them by name and does the wiring.
            MissionCharacterBuilder.EnsureTeacherAndMother();
            MissionMemoryStageBuilder.EnsureStage();
            WireOtherActors(layer);
            EnsureMissionOfferZone(staging);
            FixCameraCollision(layer);
            FixCameraCulling();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Scene wired — {SetupStamp}. Save the scene (Ctrl+S), then press Play.");
        }

        /// <summary>
        /// Just the Teacher and the Mother, so the step is runnable on its own and reports what it
        /// did. Placing them is part of Wire Open Scene, but a menu item that does one thing is
        /// what you want when the question is "did that actually run?".
        /// </summary>
        [MenuItem("ShinyMinds/Setup/Place Teacher And Mother")]
        public static void PlaceTeacherAndMother()
        {
            MissionCharacterBuilder.EnsureTeacherAndMother();
            WireOtherActors(EnsureCameraIgnoreLayer());
            FixCameraCulling();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Teacher / Mother pass done — {SetupStamp}. Read the lines above: each " +
                      "character logs either where it was placed or why it could not be. " +
                      "Save the scene.");
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

        static int EnsureCameraIgnoreLayer() => EnsureLayer("CameraIgnore");

        static void WirePlayer(GameObject player, int layer)
        {
            var animator = player.GetComponent<Animator>();
            var controller = player.GetComponent<CharacterController>();

            var mover = GetOrAdd<ActorMover>(player);
            Wire(mover,
                ("animator", animator),
                ("characterController", controller),
                // Her own speeds, so a cutscene walks her at the pace the player is used to
                // rather than at the scale-multiplied mission speeds. See ActorMover.SpeedFor.
                ("playerController", player.GetComponent<PlayerController>()),
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
            //
            // autoStartMission is cleared deliberately: the city is open world, so nothing seizes
            // control on load. MissionOffer_School offers the mission when the player walks up to
            // the gate and MissionTrigger starts it only if they accept.
            var runner = system.GetComponent<MissionRunner>();
            Wire(runner,
                ("playerTransform", player.transform),
                ("autoStartMission", null));

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

        /// <summary>
        /// The zone at the school gate that offers mission 01. Placed on `m_aisha_start`, which is
        /// where the mission opens, and about 20 m from where the player spawns — so they walk
        /// there under their own steam and are asked, not commandeered.
        /// </summary>
        static void EnsureMissionOfferZone(Transform staging)
        {
            if (staging == null) return;

            const string zoneName = "MissionOffer_School";

            Transform existing = staging.Find(zoneName);
            GameObject zone;

            if (existing != null)
            {
                zone = existing.gameObject;
            }
            else
            {
                zone = new GameObject(zoneName);
                zone.transform.SetParent(staging, false);
            }

            Transform gate = FindMarkerInScene("m_aisha_start");

            if (gate != null)
                zone.transform.position = gate.position;
            else
                Debug.LogWarning("No 'm_aisha_start' marker, so the mission offer zone is at the " +
                                 "staging root. Move it to the school gate.", zone);

            var sphere = GetOrAdd<SphereCollider>(zone);
            sphere.isTrigger = true;
            sphere.radius = 7f;
            sphere.center = new Vector3(0f, 1f, 0f);

            var trigger = GetOrAdd<MissionTrigger>(zone);

            Wire(trigger,
                ("mission", AssetDatabase.LoadAssetAtPath<MissionData>(MissionAssetPath)),
                ("runner", Object.FindAnyObjectByType<MissionRunner>()),
                ("ui", Object.FindAnyObjectByType<MissionUIView>()),
                ("startImmediately", false),
                ("once", false),
                ("rearmSeconds", 1.5f));

            Debug.Log($"Mission offer zone '{zoneName}' is at {zone.transform.position} " +
                      $"with a {sphere.radius}m radius.", zone);
        }

        static Transform FindMarkerInScene(string key)
        {
            foreach (MissionMarker m in Object.FindObjectsByType<MissionMarker>(FindObjectsInactive.Include))
                if (m.MarkerKey == key) return m.transform;

            return null;
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

        /// <summary>
        /// Puts the physics-query layers back into the main camera's culling mask.
        ///
        /// `CameraIgnore` and `Vehicle` exist to be excluded from *raycasts*: one keeps camera
        /// collision from slamming forward when a character stands behind the player, the other
        /// keeps ActorMover's ground ray off car roofs. Neither is meant to hide anything.
        ///
        /// But a layer minted after a camera's culling mask was last set does not appear in that
        /// mask, and Unity gives no warning: everything moved onto the new layer simply stops
        /// rendering. That is how the Stranger — whose entire hierarchy is CameraIgnore — became
        /// invisible in ordinary gameplay, along with all the traffic on Vehicle. Only the
        /// cutscene camera (mask "everything") ever showed him.
        ///
        /// MemoryStage is deliberately NOT added: the diorama 600 m below the city must stay
        /// visible to MemoryCamera alone.
        /// </summary>
        public static void FixCameraCulling()
        {
            Camera cam = Camera.main;

            if (cam == null)
            {
                Debug.LogWarning("No enabled camera tagged MainCamera; culling mask not checked.");
                return;
            }

            int mask = cam.cullingMask;

            foreach (string layerName in new[] { "CameraIgnore", "Vehicle" })
            {
                int l = LayerMask.NameToLayer(layerName);
                if (l >= 0) mask |= 1 << l;
            }

            if (mask == cam.cullingMask)
                return;

            Undo.RecordObject(cam, "Fix camera culling mask");
            cam.cullingMask = mask;

            Debug.Log($"{cam.name}: culling mask now includes CameraIgnore and Vehicle. Anything " +
                      "on those layers — the Stranger, the traffic — was invisible to it.", cam);
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
