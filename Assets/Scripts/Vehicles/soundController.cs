using UnityEngine;

public class CarSoundController : MonoBehaviour
{
    public Transform player;

    public AudioSource engineAudio;
    public AudioSource hornAudio;

    public float playDistance = 15f;

    private float nextHornTime;

    void Start()
    {
        SetNextHornTime();
    }

    void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        // Engine sound
        if (distance < playDistance)
        {
            if (engineAudio != null && !engineAudio.isPlaying)
            {
                engineAudio.Play();
            }

            // Horn logic. Both Police cars carry only one AudioSource (engine), so
            // they have no horn to sound - skip them rather than throw every frame.
            if (hornAudio != null && Time.time >= nextHornTime)
            {
                hornAudio.Play();
                SetNextHornTime();
            }
        }
        else if (engineAudio != null && engineAudio.isPlaying)
        {
            engineAudio.Stop();
        }
    }

    void SetNextHornTime()
    {
        nextHornTime = Time.time + Random.Range(8f, 20f);
    }
}