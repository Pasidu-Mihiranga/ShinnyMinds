using ShinyMinds.Missions.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Puts the CarAI traffic on its own layer and takes that layer out of every
    /// ActorMover's ground mask.
    ///
    /// ActorMover.SnapDown casts a ray down to find the street. With groundMask left at
    /// "everything" the ray hits whatever happens to be under the actor at that instant —
    /// including a passing car roof, which reads as a 1.3m climb and gets refused, so the
    /// actor walks on through the vehicle at the wrong height:
    ///
    ///     ActorMover on 'Stranger': ground ray hit 'Pickup_Colore_0' 1.29m above the
    ///     feet — refusing to climb onto it.
    ///
    /// Moving the mission markers cannot fix that, because the traffic is moving: the
    /// obstruction is somewhere different on every run. The ray has to stop seeing cars.
    ///
    /// Re-runnable. Run it again after adding traffic or a new mission actor.
    /// </summary>
    public static class VehicleLayerSetup
    {
        const string LayerName = "Vehicle";
        const string TagManagerPath = "ProjectSettings/TagManager.asset";

        [MenuItem("ShinyMinds/Setup/Put Traffic On The Vehicle Layer")]
        public static void Run()
        {
            int layer = EnsureLayer(LayerName);

            if (layer < 0)
                return;

            int cars = 0, objects = 0;

            foreach (CarAI car in Object.FindObjectsByType<CarAI>(FindObjectsInactive.Include))
            {
                Undo.RegisterFullObjectHierarchyUndo(car.gameObject, "Vehicle layer");

                foreach (Transform t in car.GetComponentsInChildren<Transform>(true))
                {
                    // Only relayer what is still on Default. A car's minimap blip sits on
                    // MapIcon and its UI bits on UI; sweeping those onto Vehicle would drop
                    // the car off the minimap to fix an unrelated problem.
                    if (t.gameObject.layer != 0)
                        continue;

                    t.gameObject.layer = layer;
                    objects++;
                }

                cars++;
            }

            int movers = ClearMaskBit(layer);

            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"Vehicle layer {layer}: relayered {objects} objects across {cars} CarAI " +
                      $"vehicles, and cleared that bit from {movers} ActorMover ground masks. " +
                      "Save the scene.");
        }

        /// <summary>Drops the vehicle bit from every ActorMover, in the scene and in prefabs.</summary>
        static int ClearMaskBit(int layer)
        {
            int count = 0;

            foreach (ActorMover mover in Object.FindObjectsByType<ActorMover>(FindObjectsInactive.Include))
                if (ClearOn(mover, layer))
                    count++;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    continue;

                foreach (ActorMover mover in prefab.GetComponentsInChildren<ActorMover>(true))
                {
                    if (!ClearOn(mover, layer))
                        continue;

                    EditorUtility.SetDirty(prefab);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        static bool ClearOn(ActorMover mover, int layer)
        {
            var so = new SerializedObject(mover);
            SerializedProperty mask = so.FindProperty("groundMask");

            if (mask == null)
                return false;

            int cleared = mask.intValue & ~(1 << layer);

            if (cleared == mask.intValue)
                return false;

            mask.intValue = cleared;
            so.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Returns the layer index, creating the layer if the project lacks it.</summary>
        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);

            if (existing >= 0)
                return existing;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);

            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"Could not open {TagManagerPath} to add the '{name}' layer.");
                return -1;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // 0-7 are Unity's; this project already spends 3 and 6 on MapIcon and
            // CameraIgnore, so take the first free user slot rather than guessing.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);

                if (!string.IsNullOrEmpty(slot.stringValue))
                    continue;

                slot.stringValue = name;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.Log($"Created layer {i} '{name}'.");
                return i;
            }

            Debug.LogError($"No free layer slot for '{name}'. Free one in Project Settings > Tags and Layers.");
            return -1;
        }
    }
}
