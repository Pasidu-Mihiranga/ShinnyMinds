using ShinyMinds.Missions.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ShinyMinds.Missions.EditorTools.MissionEditorUtil;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Builds the memory stage: the two stand-ins, their light, and the camera that
    /// renders them into the texture the memory bubble displays.
    ///
    /// The set is parked far below the city on its own layer rather than tucked behind
    /// the school, because a memory must never be reachable, walkable, or visible from
    /// the playable world — and the player can go anywhere on the road.
    /// </summary>
    public static class MissionMemoryStageBuilder
    {
        public const string RenderTexturePath = "Assets/Rendering/MemoryStage.renderTexture";

        /// <summary>Drop a backdrop image here and rebuild; any texture in it is used.</summary>
        const string ArtFolder = "Assets/Art/Memory";
        const string BackdropMaterialPath = ArtFolder + "/MemoryBackdrop.mat";

        const string StageObjectName = "MemoryStage";
        const string LayerName = "MemoryStage";

        // Models are resolved by GUID, not by path: the player's model is "GİRL 1.FBX",
        // whose dotted capital İ does not survive every editor and shell round trip.
        const string AishaModelGuid = "d88e7d4ce8ee51f42ac8f488e56827c0";   // characters/GİRL 1.FBX
        const string MotherModelGuid = "0c8fc4e4d5a49b441a31e2c1a5e87088";  // characters/Mother.fbx

        // Scale 5 for the child and 2 for the adult, matching how both already stand in
        // the scene. Getting these wrong is instantly visible: a giant in the bubble.
        const float AishaScale = 5f;
        const float MotherScale = 2f;

        const float StageDepth = -600f;   // metres below the city

        // ------------------------------------------------------------------ menu items

        [MenuItem("ShinyMinds/Setup/7. Build Memory Stage")]
        public static void BuildStageMenu()
        {
            GameObject stage = EnsureStage();
            if (stage == null) return;

            Selection.activeObject = stage;
            EditorGUIUtility.PingObject(stage);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Throws the existing set away and builds a fresh one. Use this after changing
        /// the models or the framing constants; plain "Build" deliberately keeps any
        /// camera nudges you made by hand.
        /// </summary>
        [MenuItem("ShinyMinds/Setup/Rebuild Memory Stage From Scratch")]
        public static void RebuildStage()
        {
            GameObject existing = FindStage();
            if (existing != null) Object.DestroyImmediate(existing);

            BuildStageMenu();
        }

        /// <summary>
        /// The memory bubble is half prefab and half scene: its size, position and speech
        /// balloons live in MissionUI.prefab, while the framing of the two stand-ins lives
        /// in the scene. Editing the builders changes neither until they are re-run, and
        /// running only one of the two is a confusing half-applied result — so this does
        /// both.
        /// </summary>
        [MenuItem("ShinyMinds/Setup/Apply Memory Bubble Changes")]
        public static void ApplyMemoryChanges()
        {
            MissionUIBuilder.Build();
            RebuildStage();

            Debug.Log("Memory bubble rebuilt: MissionUI.prefab and the memory stage are " +
                      "both up to date. Save the scene (Ctrl+S).");
        }

        // ---------------------------------------------------------------------- build

        /// <summary>Returns the stage in the open scene, building it if it is missing.</summary>
        public static GameObject EnsureStage()
        {
            GameObject existing = FindStage();
            if (existing != null)
            {
                WireRunner(existing.GetComponent<MemoryStage>());
                return existing;
            }

            int layer = EnsureLayer(LayerName);
            RenderTexture rt = EnsureRenderTexture();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                MissionAnimatorBuilder.MissionActorControllerPath);

            if (controller == null)
                Debug.LogWarning("MissionActorAnimator.controller not found — run 'Setup/4. Build Animator " +
                                 "Controllers' and then rebuild the memory stage, or the stand-ins will not move.");

            var root = new GameObject(StageObjectName);
            root.transform.position = new Vector3(0f, StageDepth, 0f);
            var stage = root.AddComponent<MemoryStage>();

            var diorama = new GameObject("Diorama");
            diorama.transform.SetParent(root.transform, false);

            Camera cam = BuildCamera(diorama.transform, layer, rt);
            BuildBackdrop(diorama.transform, layer);
            BuildLights(diorama.transform);

            // Aisha on the left, her mother on the right, both turned three-quarters
            // towards the lens so the bubble reads as a conversation rather than two
            // profiles. Rotation 180 faces the camera; 135 / 225 splits the difference.
            // Two metres apart, which is wide for a conversation but leaves the middle of
            // the frame clear rather than stacking them into one silhouette.
            Animator aisha = BuildStandIn(diorama.transform, "Aisha_Memory", AishaModelGuid,
                                          new Vector3(-1.15f, 0f, 0f), 135f, AishaScale, layer, controller);

            Animator mother = BuildStandIn(diorama.transform, "Mother_Memory", MotherModelGuid,
                                           new Vector3(1.20f, 0f, 0f), 225f, MotherScale, layer, controller);

            Wire(stage,
                ("diorama", diorama),
                ("stageCamera", cam),
                ("leftActor", aisha),
                ("rightActor", mother));

            HideStageFromOtherCameras(cam, layer);
            WireRunner(stage);

            // The runner switches it on for the remembered lines and off again after.
            diorama.SetActive(false);

            Debug.Log("Built the memory stage. If the two of them sit oddly in the bubble, " +
                      "select MemoryStage/Diorama/MemoryCamera and nudge it — the Game view " +
                      "will not show this camera, so preview the RenderTexture asset instead.", root);

            return root;
        }

        /// <summary>The texture the stage camera draws into and the memory bubble displays.</summary>
        public static RenderTexture EnsureRenderTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (existing != null) return existing;

            // 16:9, and only as large as the bubble it fills — this renders every frame
            // the memory is open, on top of the game's own camera.
            var rt = new RenderTexture(1152, 648, 24)
            {
                name = "MemoryStage",
                antiAliasing = 4,       // no post-processing down here to hide hard edges
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
            };

            EnsureFolder(RenderTexturePath);
            AssetDatabase.CreateAsset(rt, RenderTexturePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created {RenderTexturePath}");
            return rt;
        }

        // -------------------------------------------------------------------- pieces

        static Camera BuildCamera(Transform parent, int layer, RenderTexture rt)
        {
            var go = new GameObject("MemoryCamera");
            go.transform.SetParent(parent, false);
            // Backed off, and aimed so both heads sit around 40% down the frame: the speech
            // balloons hang across the top of it, and a balloon over someone's face is
            // worse than a little empty floor. The oval mask also eats the corners, so the
            // pair has to keep away from them.
            go.transform.localPosition = new Vector3(0f, 1.5f, -5.6f);
            go.transform.localRotation = Quaternion.Euler(0.5f, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Warm paper rather than the city's sky: a memory should not look like a
            // second window onto the same afternoon.
            cam.backgroundColor = new Color(0.99f, 0.95f, 0.88f, 1f);
            cam.cullingMask = layer >= 0 ? 1 << layer : ~0;
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.targetTexture = rt;
            cam.useOcclusionCulling = false;

            // A second AudioListener halves the mix and spams warnings; Main Camera
            // keeps the only one.
            AudioListener stray = go.GetComponent<AudioListener>();
            if (stray != null) Object.DestroyImmediate(stray);

            return cam;
        }

        /// <summary>
        /// The flat illustration standing in for home, on a quad behind the pair.
        ///
        /// It is unlit on purpose: the two point lights are there to model the 3D figures,
        /// and letting them fall across a drawing would only reveal that it is a picture
        /// on a plane. Any texture dropped into <see cref="ArtFolder"/> is used, so the
        /// backdrop can be swapped without touching this file.
        /// </summary>
        static void BuildBackdrop(Transform parent, int layer)
        {
            Texture2D art = FindBackdropTexture();
            if (art == null)
            {
                Debug.Log($"No backdrop texture in {ArtFolder} — the memory will use the " +
                          "camera's plain paper colour. Drop an image in that folder and " +
                          "rebuild to set the scene.");
                return;
            }

            Material mat = BuildBackdropMaterial(art);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Backdrop";
            go.transform.SetParent(parent, false);

            // Behind the pair, and large enough to overrun the frame on every side: the
            // width is fitted to the view and the height follows the image's own aspect,
            // so nothing is stretched and the crop lands in the illustration's margins.
            go.transform.localPosition = new Vector3(0f, 1.31f, 3.0f);
            go.transform.localScale = new Vector3(9.6f, 9.6f * art.height / art.width, 1f);

            SetLayerRecursive(go, layer);

            // A primitive arrives with a collider. Nothing may collide down here — the
            // player is 600 metres up and this is scenery inside a picture.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Texture2D FindBackdropTexture()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) return null;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
            if (guids.Length == 0) return null;

            if (guids.Length > 1)
                Debug.LogWarning($"More than one texture in {ArtFolder}; using the first. " +
                                 "Keep only the backdrop there to make the choice obvious.");

            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static Material BuildBackdropMaterial(Texture2D art)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(BackdropMaterialPath);
            Material mat = existing != null ? existing : new Material(shader);

            mat.shader = shader;
            mat.mainTexture = art;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", art);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            // Double-sided, so a quad that ends up facing away is still a backdrop rather
            // than an invisible one.
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

            if (existing == null)
            {
                EnsureFolder(BackdropMaterialPath);
                AssetDatabase.CreateAsset(mat, BackdropMaterialPath);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// Two point lights rather than a directional one: their range cannot reach the
        /// city 600 metres above, so the diorama is lit no matter what the scene's own
        /// lighting is doing, and nothing the player can see is touched.
        /// </summary>
        static void BuildLights(Transform parent)
        {
            AddLight(parent, "KeyLight", new Vector3(-2.2f, 2.6f, -3.0f),
                     new Color(1f, 0.96f, 0.88f), 3.4f);

            AddLight(parent, "FillLight", new Vector3(2.4f, 1.8f, -3.2f),
                     new Color(0.88f, 0.93f, 1f), 1.5f);
        }

        static void AddLight(Transform parent, string name, Vector3 localPos, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 14f;
            // Flat and shadowless, like the storybook art this bubble is imitating.
            light.shadows = LightShadows.None;
        }

        static Animator BuildStandIn(Transform parent, string name, string modelGuid,
                                     Vector3 localPos, float yaw, float scale, int layer,
                                     AnimatorController controller)
        {
            string path = AssetDatabase.GUIDToAssetPath(modelGuid);
            var model = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
            {
                Debug.LogError($"No model for '{name}' (guid {modelGuid}). The memory bubble will be empty.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;

            // Unpacked so the animator override below is saved with the scene rather
            // than fighting the imported model prefab.
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            SetLayerRecursive(go, layer);

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            // The stand-ins never travel; root motion would only walk them out of frame.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return animator;
        }

        // ------------------------------------------------------------------- helpers

        static GameObject FindStage()
        {
            MemoryStage existing = Object.FindAnyObjectByType<MemoryStage>(FindObjectsInactive.Include);
            return existing != null ? existing.gameObject : null;
        }

        /// <summary>
        /// Belt and braces on top of the 600-metre drop: no other camera in the scene
        /// renders the memory layer, so the stand-ins cannot appear in the world even if
        /// someone later moves the set.
        /// </summary>
        static void HideStageFromOtherCameras(Camera stageCamera, int layer)
        {
            if (layer < 0) return;

            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (cam == stageCamera) continue;

                int without = cam.cullingMask & ~(1 << layer);
                if (without == cam.cullingMask) continue;

                cam.cullingMask = without;
                EditorUtility.SetDirty(cam);
            }
        }

        static void WireRunner(MemoryStage stage)
        {
            if (stage == null) return;

            var runner = Object.FindAnyObjectByType<MissionRunner>(FindObjectsInactive.Include);
            if (runner != null) Wire(runner, ("memoryStage", stage));
        }
    }
}
