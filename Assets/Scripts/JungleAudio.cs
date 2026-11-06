using System.Collections.Generic;
using UnityEngine;

/// <summary>Small, self-contained jungle soundtrack and effects synthesized at runtime.</summary>
public sealed class JungleAudio : MonoBehaviour
{
    const int SampleRate = 22050;
    const float MusicVolume = .65f;
    static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
    static AudioClip musicClip, coinClip, jumpClip, damageClip, checkpointClip, finishClip, selectionClip, powerupClip, buttonClip, gameOverClip;

    AudioSource musicSource;
    AudioSource effectsSource;
    bool soundEnabled = true;

    public bool SoundEnabled => soundEnabled;

    public void Initialize(Camera sceneCamera)
    {
        if (sceneCamera && !sceneCamera.GetComponent<AudioListener>())
            sceneCamera.gameObject.AddComponent<AudioListener>();

        if (!musicSource)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0;
            musicSource.volume = MusicVolume;
            musicSource.clip = GetMusic();
        }

        if (!effectsSource)
        {
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.loop = false;
            effectsSource.spatialBlend = 0;
            effectsSource.volume = .75f;
        }

        if (soundEnabled && !musicSource.isPlaying)
            musicSource.Play();
    }

    public void SetSoundEnabled(bool enabled)
    {
        soundEnabled = enabled;
        if (!musicSource) return;
        musicSource.mute = !enabled;
        effectsSource.mute = !enabled;
        if (enabled && !musicSource.isPlaying)
            musicSource.Play();
    }

    public void PlayCoin() => Play(GetCoin());
    public void PlayJump() => Play(GetJump());
    public void PlayDamage() => Play(GetDamage());
    public void PlayCheckpoint() => Play(GetCheckpoint());
    public void PlayFinish() => Play(GetFinish());
    public void PlaySelection() => Play(GetSelection());
    public void PlayPowerup() => Play(GetPowerup());
    public void PlayButtonClick() => Play(GetButtonClick());
    public void PlayGameOver() => Play(GetGameOver());

    void Play(AudioClip clip)
    {
        if (soundEnabled && effectsSource && clip)
            effectsSource.PlayOneShot(clip);
    }

    static AudioClip GetMusic()
    {
        if (musicClip) return musicClip;
        const float beatSeconds = .62f;
        const int beats = 32;
        float duration = beatSeconds * beats;
        int count = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[count * 2];
        int[][] chords =
        {
            new[] { 48, 55, 60, 62, 64, 67 }, // C add9
            new[] { 45, 52, 55, 57, 60, 64 }, // Am7
            new[] { 41, 48, 52, 53, 57, 60 }, // Fmaj7
            new[] { 43, 50, 55, 59, 62, 67 }, // G
            new[] { 48, 55, 60, 64, 67, 72 }, // C
            new[] { 43, 50, 55, 59, 62, 67 }, // G
            new[] { 38, 45, 48, 50, 53, 57 }, // Dm7
            new[] { 43, 50, 53, 55, 60, 62 }  // G sus
        };
        int[] melody =
        {
            72,-1,76,79,-1,76,74,-1, 72,76,79,-1,83,79,76,-1,
            72,-1,76,81,79,-1,76,74, 67,-1,74,76,-1,79,76,-1,
            72,76,-1,79,84,79,-1,76, 74,-1,76,79,-1,83,79,76,
            84,-1,83,79,76,74,-1,72, 67,-1,72,74,76,-1,72,-1
        };
        const float eighthSeconds = beatSeconds * .5f;
        const int crossfadeFrames = SampleRate / 14;
        uint noiseState = 0xA341316Cu;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            int beat = Mathf.FloorToInt(t / beatSeconds);
            int bar = beat / 4;
            int beatInBar = beat % 4;
            int chordIndex = bar % chords.Length;
            float beatAge = Mathf.Repeat(t, beatSeconds);
            float padIn = Mathf.SmoothStep(0, 1, Mathf.Clamp01(beatAge / .2f));
            float padOut = 1f - .12f * Mathf.Clamp01(beatAge / beatSeconds);
            float pad = 0;
            foreach (int note in chords[chordIndex])
            {
                float frequency = MidiToFrequency(note);
                pad += Mathf.Sin(2 * Mathf.PI * frequency * t) * .0055f;
                pad += Mathf.Sin(2 * Mathf.PI * frequency * 2.003f * t) * .0012f;
            }

            int step = Mathf.FloorToInt(t / eighthSeconds);
            int stepInBar = step % 8;
            float stepAge = Mathf.Repeat(t, eighthSeconds);
            float lead = 0;
            int melodyNote = melody[step % melody.Length];
            if (melodyNote >= 0)
            {
                float envelope = Mathf.Exp(-stepAge * 6.2f) * Mathf.Clamp01((eighthSeconds - stepAge) * 16f);
                float frequency = MidiToFrequency(melodyNote);
                float phase = 2 * Mathf.PI * frequency * t;
                float tone = Mathf.Sin(phase) + .3f * Mathf.Sin(phase * 2.01f) + .08f * Mathf.Sin(phase * 3.02f);
                lead = tone * envelope * .027f;
            }

            // Quiet offbeat marimba notes add a second, interlocking musical phrase.
            float marimba = 0;
            if (stepInBar % 2 == 1)
            {
                int chordTone = 2 + (stepInBar / 2) % 4;
                float frequency = MidiToFrequency(chords[chordIndex][chordTone] + 12);
                float envelope = Mathf.Exp(-stepAge * 8.5f) * Mathf.Clamp01((eighthSeconds - stepAge) * 18f);
                float phase = 2 * Mathf.PI * frequency * t;
                marimba = (Mathf.Sin(phase) + .2f * Mathf.Sin(phase * 2.02f)) * envelope * .014f;
            }

            int bassNote = chords[chordIndex][stepInBar == 3 || stepInBar == 7 ? 1 : 0] - 12;
            float bassPulse = .72f + .28f * Mathf.Exp(-beatAge * 9f);
            float bass = (Mathf.Sin(2 * Mathf.PI * MidiToFrequency(bassNote) * t) + .12f * Mathf.Sin(4 * Mathf.PI * MidiToFrequency(bassNote) * t)) * .032f * bassPulse;

            float kick = 0;
            if (beatInBar == 0 || beatInBar == 2 || (bar % 2 == 1 && beatInBar == 3))
            {
                float phase = 2 * Mathf.PI * (46f * beatAge + 92f * (1f - Mathf.Exp(-22f * beatAge)) / 22f);
                kick = Mathf.Sin(phase) * Mathf.Exp(-beatAge * 17f) * .12f;
            }

            float tom = 0;
            if (beatInBar == 1 || beatInBar == 3)
            {
                float phase = 2 * Mathf.PI * (148f * beatAge + 28f * (1f - Mathf.Exp(-18f * beatAge)) / 18f);
                tom = (Mathf.Sin(phase) + .18f * Mathf.Sin(2 * Mathf.PI * 410f * beatAge)) * Mathf.Exp(-beatAge * 24f) * .052f;
            }

            noiseState = unchecked(1664525u * noiseState + 1013904223u);
            float noise = ((noiseState >> 8) / 16777215f) * 2f - 1f;
            float shakerAccent = step % 2 == 0 ? .62f : 1f;
            float shaker = noise * Mathf.Exp(-stepAge * 58f) * .016f * shakerAccent;

            float center = pad * padIn * padOut + bass + kick + tom;
            float pan = (stepInBar < 4 ? -.2f : .2f);
            float left = center + lead * (1f - pan) + marimba * (1f - pan) + shaker * (step % 2 == 0 ? 1f : .62f);
            float right = center + lead * (1f + pan) + marimba * (1f + pan) + shaker * (step % 2 == 0 ? .62f : 1f);
            samples[i * 2] = Mathf.Clamp(left, -.42f, .42f);
            samples[i * 2 + 1] = Mathf.Clamp(right, -.42f, .42f);
        }

        for (int i = 0; i < crossfadeFrames; i++)
        {
            float blend = (i + 1f) / crossfadeFrames;
            float tailGain = Mathf.Cos(blend * Mathf.PI * .5f);
            float headGain = Mathf.Sin(blend * Mathf.PI * .5f);
            int tail = (count - crossfadeFrames + i) * 2;
            int head = i * 2;
            for (int channel = 0; channel < 2; channel++)
                samples[tail + channel] = samples[tail + channel] * tailGain + samples[head + channel] * headGain;
        }

        musicClip = CreateStereoClip("Jungle Quest • Rainforest Adventure Mix", samples);
        return musicClip;
    }

    static AudioClip GetCoin()
    {
        if (coinClip) return coinClip;
        coinClip = MakeSweep("Sun coin", .38f, 760, 1180, .19f, .07f);
        return coinClip;
    }

    static AudioClip GetJump()
    {
        if (jumpClip) return jumpClip;
        jumpClip = MakeSweep("Explorer jump", .22f, 270, 470, .12f, .035f);
        return jumpClip;
    }

    static AudioClip GetDamage()
    {
        if (damageClip) return damageClip;
        damageClip = MakeSweep("Explorer hit", .34f, 190, 68, .18f, .025f);
        return damageClip;
    }

    static AudioClip GetCheckpoint()
    {
        if (checkpointClip) return checkpointClip;
        checkpointClip = MakeChord("Checkpoint reached", .78f, new[] { 523.25f, 659.25f, 783.99f }, .13f);
        return checkpointClip;
    }

    static AudioClip GetFinish()
    {
        if (finishClip) return finishClip;
        finishClip = MakeChord("Treasure found", 1.25f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, .15f);
        return finishClip;
    }

    static AudioClip GetSelection()
    {
        if (selectionClip) return selectionClip;
        selectionClip = MakeSweep("Explorer selected", .13f, 570, 760, .08f, .01f);
        return selectionClip;
    }

    static AudioClip GetPowerup()
    {
        if (powerupClip) return powerupClip;
        powerupClip = MakeChord("Powerup acquired", .48f, new[] { 659.25f, 783.99f, 1046.5f }, .14f);
        return powerupClip;
    }

    static AudioClip GetButtonClick()
    {
        if (buttonClip) return buttonClip;
        buttonClip = MakeSweep("Jungle menu click", .1f, 430, 590, .075f, .008f);
        return buttonClip;
    }

    static AudioClip GetGameOver()
    {
        if (gameOverClip) return gameOverClip;
        gameOverClip = MakeChord("Expedition ended", .9f, new[] { 392f, 311f, 233f }, .16f);
        return gameOverClip;
    }

    static AudioClip MakeSweep(string name, float duration, float from, float to, float volume, float tail)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[count];
        float phase = 0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float progress = t / duration;
            float frequency = Mathf.Lerp(from, to, progress);
            phase += 2 * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Exp(-t * (3.3f + tail * 12f)) * Mathf.Clamp01((duration - t) * 18f);
            float tone = Mathf.Sin(phase) + .24f * Mathf.Sin(phase * 2.01f);
            float noise = (Mathf.PerlinNoise(i * .019f, 2.71f) - .5f) * tail * (1f - progress);
            samples[i] = (tone * volume + noise) * envelope;
        }
        return CreateClip(name, samples);
    }

    static AudioClip MakeChord(string name, float duration, float[] frequencies, float volume)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = Mathf.Min(Mathf.Clamp01(t * 10f), Mathf.Exp(-t * 1.25f)) * Mathf.Clamp01((duration - t) * 12f);
            float tone = 0;
            for (int n = 0; n < frequencies.Length; n++)
            {
                float delayed = Mathf.Max(0, t - n * .09f);
                tone += Mathf.Sin(2 * Mathf.PI * frequencies[n] * delayed) / (n + 1f);
            }
            samples[i] = tone * volume / frequencies.Length * envelope;
        }
        return CreateClip(name, samples);
    }

    static AudioClip CreateClip(string name, float[] samples)
    {
        if (Clips.TryGetValue(name, out var cached) && cached) return cached;
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        Clips[name] = clip;
        return clip;
    }

    static AudioClip CreateStereoClip(string name, float[] interleavedSamples)
    {
        if (Clips.TryGetValue(name, out var cached) && cached) return cached;
        var clip = AudioClip.Create(name, interleavedSamples.Length / 2, 2, SampleRate, false);
        clip.SetData(interleavedSamples, 0);
        Clips[name] = clip;
        return clip;
    }

    static float MidiToFrequency(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
}
