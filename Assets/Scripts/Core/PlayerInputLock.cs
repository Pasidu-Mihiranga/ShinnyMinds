using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinyMinds.Core
{
    /// <summary>
    /// Single source of truth for "is the player allowed to control the game" and
    /// "is the OS cursor free". Refcounted by owner object so nested owners
    /// (a mission and a Groq NPC) can never unlock each other.
    /// </summary>
    public static class PlayerInputLock
    {
        static readonly HashSet<object> gameplayOwners = new HashSet<object>();
        static readonly HashSet<object> cursorOwners = new HashSet<object>();

        public static bool IsLocked => gameplayOwners.Count > 0;
        public static bool IsCursorFree => cursorOwners.Count > 0;

        /// <summary>Raised when the gameplay lock goes from none-held to held, or back.</summary>
        public static event Action<bool> GameplayLockChanged;

        public static void Acquire(object owner)
        {
            if (owner == null || !gameplayOwners.Add(owner))
                return;

            if (gameplayOwners.Count == 1)
                GameplayLockChanged?.Invoke(true);
        }

        public static void Release(object owner)
        {
            if (owner == null || !gameplayOwners.Remove(owner))
                return;

            if (gameplayOwners.Count == 0)
                GameplayLockChanged?.Invoke(false);
        }

        public static void PushCursorFree(object owner)
        {
            if (owner != null && cursorOwners.Add(owner))
                ApplyCursor();
        }

        public static void PopCursorFree(object owner)
        {
            if (owner != null && cursorOwners.Remove(owner))
                ApplyCursor();
        }

        /// <summary>
        /// Re-assert cursor state. Unity silently releases the cursor lock on focus
        /// loss / alt-tab, so this must be callable at any time.
        /// </summary>
        public static void ApplyCursor()
        {
            Cursor.lockState = IsCursorFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsCursorFree;
        }

        /// <summary>
        /// Hard reset. Used by mission retry so a mid-cutscene abort can never
        /// strand the player with the controls disabled.
        /// </summary>
        public static void ResetAll()
        {
            bool wasLocked = IsLocked;

            gameplayOwners.Clear();
            cursorOwners.Clear();

            if (wasLocked)
                GameplayLockChanged?.Invoke(false);

            ApplyCursor();
        }

        // Without this, "Enter Play Mode Options -> Disable Domain Reload" would carry
        // stale owners into the next session and permanently freeze the player.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            gameplayOwners.Clear();
            cursorOwners.Clear();
            GameplayLockChanged = null;
        }
    }
}
