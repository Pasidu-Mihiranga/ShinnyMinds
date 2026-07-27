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
            if (!engineAudio.isPlaying)
            {
                engineAudio.Play();
            }

            // Horn logic
            if (Time.time >= nextHornTime)
            {
                hornAudio.Play();
                SetNextHornTime();
            }
        }
        else
        {
            engineAudio.Stop();
        }
    }

    void SetNextHornTime()
    {
        nextHornTime = Time.time + Random.Range(8f, 20f);
    }
}