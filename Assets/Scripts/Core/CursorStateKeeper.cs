using UnityEngine;

namespace ShinyMinds.Core
{
    /// <summary>
    /// Re-asserts the cursor lock. Unity drops CursorLockMode.Locked whenever the
    /// application loses focus, so without this an alt-tab leaves the player with a
    /// free cursor and a camera that no longer responds predictably.
    /// Put one of these on the MissionSystem root.
    /// </summary>
    public class CursorStateKeeper : MonoBehaviour
    {
        void Start()
        {
            PlayerInputLock.ApplyCursor();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                PlayerInputLock.ApplyCursor();
        }
    }
}
