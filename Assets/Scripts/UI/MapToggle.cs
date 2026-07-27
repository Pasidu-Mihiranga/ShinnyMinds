using UnityEngine;

public class MapToggle : MonoBehaviour
{
    // MAIN GAME CAMERA
    public Camera mainCamera;

    // TOP MAP CAMERA
    public Camera mapCamera;

    // MAP STATE
    private bool mapEnabled = false;

    void Update()
    {
        // PRESS M
        if (Input.GetKeyDown(KeyCode.M))
        {
            mapEnabled = !mapEnabled;

            // SWITCH CAMERAS
            mainCamera.gameObject.SetActive(!mapEnabled);

            mapCamera.gameObject.SetActive(mapEnabled);
        }
    }
}