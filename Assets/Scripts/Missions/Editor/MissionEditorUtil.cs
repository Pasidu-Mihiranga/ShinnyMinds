using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Shared helpers for the mission setup builders. Nothing here runs at play time.
    /// </summary>
    public static class MissionEditorUtil
    {
        public const string PrefabRoot = "Assets/Prefabs";

        // ------------------------------------------------------------ serialization

        /// <summary>
        /// Assigns private [SerializeField] fields, which is the only way to wire these
        /// components from a script. Field names must match the C# exactly.
        /// </summary>
        public static void Wire(Object target, params (string field, object value)[] pairs)
        {
            var so = new SerializedObject(target);

            foreach ((string field, object value) in pairs)
            {
                SerializedProperty p = so.FindProperty(field);

                if (p == null)
                {
                    Debug.LogWarning($"Wire: '{target.GetType().Name}' has no serialized field '{field}'.");
                    continue;
                }

                switch (value)
                {
                    case null: p.objectReferenceValue = null; break;
                    case Object o: p.objectReferenceValue = o; break;
                    case string s: p.stringValue = s; break;
                    case bool b: p.boolValue = b; break;
                    case int i: p.intValue = i; break;
                    case float f: p.floatValue = f; break;
                    case Color c: p.colorValue = c; break;
                    case Vector3 v: p.vector3Value = v; break;
                    case LayerMask m: p.intValue = m.value; break;
                    default:
                        Debug.LogWarning($"Wire: unsupported value type {value.GetType()} for '{field}'.");
                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Fills a serialized List&lt;T&gt; of object references.</summary>
        public static void WireList(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);

            if (p == null || !p.isArray)
            {
                Debug.LogWarning($"WireList: '{target.GetType().Name}.{field}' is not a serialized list.");
                return;
            }

            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // -------------------------------------------------------------------- UI

        public static GameObject Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        /// <summary>Stretch to fill the parent, with an optional uniform inset.</summary>
        public static RectTransform Stretch(GameObject go, float inset = 0f)
        {
            RectTransform rt = RT(go);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Anchor to an edge/corner. Pivot matches the anchor.</summary>
        public static RectTransform Anchor(GameObject go, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            RectTransform rt = RT(go);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        /// <summary>Full-width bar pinned to the top or bottom.</summary>
        public static RectTransform HorizontalBar(GameObject go, bool top, float height)
        {
            RectTransform rt = RT(go);
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static Image AddImage(GameObject go, Color color, bool raycastTarget)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return img;
        }

        public static TextMeshProUGUI AddText(GameObject go, string text, float size,
                                              TextAlignmentOptions align, Color color,
                                              FontStyles style = FontStyles.Normal)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // ---------------------------------------------------------------- assets

        public static void EnsureFolder(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        /// <summary>Saves a scene object as a prefab, then removes the temporary instance.</summary>
        public static GameObject SavePrefab(GameObject temp, string assetPath)
        {
            EnsureFolder(assetPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, assetPath, out bool ok);

            if (!ok || prefab == null)
                Debug.LogError($"Failed to save prefab at '{assetPath}'.");

            Object.DestroyImmediate(temp);
            return prefab;
        }
    }
}
