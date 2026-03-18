using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundObject : MonoBehaviour
{
    [HideInInspector] public bool isStartPlay;
    public AudioSource _audio { get; set; }
    float _lastAppliedVolume = float.NaN;

    AudioSource GetAudioSource()
    {
        if (_audio == null)
        {
            _audio = GetComponent<AudioSource>();
        }

        return _audio;
    }

    void ApplyVolumeIfNeeded()
    {
        AudioSource audioSource = GetAudioSource();
        if (audioSource == null || ContinuousController.instance == null)
        {
            return;
        }

        float targetVolume = ContinuousController.instance.SEVolume * 0.5f * 0.8f;
        if (Mathf.Approximately(_lastAppliedVolume, targetVolume))
        {
            return;
        }

        audioSource.volume = targetVolume;
        _lastAppliedVolume = targetVolume;
    }

    public void PlaySE(AudioClip clip)
    {
        _audio = GetAudioSource();

        _audio.clip = clip;

        _lastAppliedVolume = float.NaN;
        ApplyVolumeIfNeeded();

        _audio.Play();
        isStartPlay = true;
    }

    private void Update()
    {
        if (ContinuousController.instance != null)
        {
            ApplyVolumeIfNeeded();
        }

        if (_audio != null && !_audio.isPlaying && isStartPlay)
        {
            Destroy(this.gameObject);
        }
    }
}
