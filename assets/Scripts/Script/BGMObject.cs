using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(AudioSource))]
public class BGMObject : MonoBehaviour
{
    public AudioSource _audio { get; set; }
    public bool isPlaying { get; set; } = false;
    bool isFading { get; set; } = false;
    float _lastAppliedVolume = float.NaN;
    private void Start()
    {

    }

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

        float targetVolume = ContinuousController.instance.BGMVolume * 0.25f * 0.8f;
        if (Mathf.Approximately(_lastAppliedVolume, targetVolume))
        {
            return;
        }

        audioSource.volume = targetVolume;
        _lastAppliedVolume = targetVolume;
    }

    public void StopPlayBGM()
    {
        _audio = GetAudioSource();

        _audio.Stop();

        _audio.clip = null;

        isPlaying = false;
        _lastAppliedVolume = float.NaN;
    }

    public void StartPlayBGM(AudioClip clip)
    {
        _audio = GetAudioSource();

        if (clip != null)
        {
            _audio.clip = clip;
        }

        _lastAppliedVolume = float.NaN;
        ApplyVolumeIfNeeded();

        _audio.Play();

        isPlaying = true;
    }

    private void Update()
    {
        if (ContinuousController.instance != null)
        {
            if (_audio != null && isPlaying && !isFading)
            {
                ApplyVolumeIfNeeded();
            }
        }
    }

    public IEnumerator FadeOut(float duration)
    {
        _audio = GetAudioSource();

        bool end = false;
        isFading = true;

        var sequence = DOTween.Sequence();

        sequence
            .Append(DOTween.To(() => _audio.volume, (value) => _audio.volume = value, 0, duration))
            .AppendCallback(() => end = true);

        sequence.Play();

        yield return new WaitWhile(() => !end);

        isPlaying = false;
        isFading = false;
        _lastAppliedVolume = float.NaN;
    }
}
