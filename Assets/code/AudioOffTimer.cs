using UnityEngine;
using System.Collections;

public class AudioDelayPlay : MonoBehaviour
{
    public AudioSource audioSource;
    public float delaySeconds = 4f;   // change this in Inspector anytime

    void Start()
    {
        StartCoroutine(PlayAfterDelay());
    }

    IEnumerator PlayAfterDelay()
    {
        yield return new WaitForSeconds(delaySeconds);
        audioSource.Play();
    }
}
