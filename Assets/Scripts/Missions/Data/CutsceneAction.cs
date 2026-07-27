using System;
using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// One step of a staged sequence. Stored with [SerializeReference], so Unity
    /// renders a type-picker dropdown in the inspector with no custom editor code.
    ///
    /// IMPORTANT: [SerializeReference] stores the concrete type by assembly +
    /// namespace + class name. Renaming or re-namespacing a subclass NULLS every
    /// stored instance with no undo. Keep this namespace fixed, keep one class per
    /// file, and add [MovedFrom] before ever renaming one.
    /// </summary>
    [Serializable]
    public abstract class CutsceneAction
    {
        [Tooltip("Start this action and immediately continue to the next one. " +
                 "Parallel actions are joined before the next sequential action runs, " +
                 "and again at the end of the list.")]
        public bool runInParallel = false;

        public abstract IEnumerator Execute(IMissionContext ctx);

        /// <summary>Shared null-guard so a half-authored asset warns instead of throwing.</summary>
        protected static IMissionActor RequireActor(IMissionContext ctx, string key, string actionName)
        {
            IMissionActor actor = ctx.GetActor(key);
            if (actor == null)
                Debug.LogWarning($"{actionName}: no actor registered with key '{key}'.");
            return actor;
        }

        protected static Transform RequireMarker(IMissionContext ctx, string key, string actionName)
        {
            Transform marker = ctx.GetMarker(key);
            if (marker == null)
                Debug.LogWarning($"{actionName}: no marker registered with key '{key}'.");
            return marker;
        }
    }
}
