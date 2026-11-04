using System.Collections.Generic;
using UnityEngine;

namespace SkyJump.Audio
{
    public enum Sfx
    {
        Click,
        Jump,
        LoseLife,
        WorldChange,
        Victory,
        Defeat,
        Break
    }

    /// <summary>
    /// Efeitos sonoros estilo chiptune (8 bits) gerados por código: combinam com o visual
    /// pixel art e não dependem de arquivos de áudio. Criado automaticamente e mantido entre cenas.
    /// O jogo funciona normalmente mesmo sem áudio.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int SampleRate = 44100;
        const string MutedKey = "SkyJump.Muted";

        static AudioManager instance;

        [Range(0f, 1f)] [SerializeField] float masterVolume = 0.35f;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource source;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set { PlayerPrefs.SetInt(MutedKey, value ? 1 : 0); AudioListener.volume = value ? 0f : 1f; }
        }

        static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public static void Play(Sfx sfx, float volume = 1f)
        {
            if (!Application.isPlaying) return;
            Instance.PlayInternal(sfx, volume);
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            AudioListener.volume = Muted ? 0f : 1f;
            Build();
        }

        void PlayInternal(Sfx sfx, float volume)
        {
            if (clips.TryGetValue(sfx, out var clip) && clip != null)
                source.PlayOneShot(clip, masterVolume * volume);
        }

        void Build()
        {
            clips[Sfx.Click] = Tone("click", new[] { 880f }, 0.05f, 0.5f, 0f);
            clips[Sfx.Jump] = Sweep("jump", 330f, 720f, 0.12f, 0.25f);
            clips[Sfx.LoseLife] = Sweep("lose", 520f, 110f, 0.45f, 0.5f);
            clips[Sfx.Break] = Noise("break", 0.18f);
            clips[Sfx.WorldChange] = Tone("world", new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.5f, 0.25f);
            clips[Sfx.Victory] = Tone("victory", new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f }, 0.12f, 0.5f, 0.25f);
            clips[Sfx.Defeat] = Tone("defeat", new[] { 392f, 330f, 262f, 196f }, 0.2f, 0.5f, 0.25f);
        }

        static float Square(float phase, float duty) => (phase % 1f) < duty ? 1f : -1f;

        static AudioClip Tone(string name, float[] notes, float noteLength, float duty, float sustainFade)
        {
            int perNote = Mathf.CeilToInt(noteLength * SampleRate);
            var data = new float[perNote * notes.Length];
            float phase = 0f;
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)perNote;
                    phase += notes[n] / SampleRate;
                    float env = Mathf.Clamp01(1f - t * (1f - sustainFade)) * Mathf.Clamp01(t * 40f);
                    if (n == notes.Length - 1) env *= 1f - t;
                    data[n * perNote + i] = Square(phase, duty) * env * 0.5f;
                }
            return Create(name, data);
        }

        static AudioClip Sweep(string name, float from, float to, float length, float duty)
        {
            int count = Mathf.CeilToInt(length * SampleRate);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                phase += Mathf.Lerp(from, to, t) / SampleRate;
                data[i] = Square(phase, duty) * (1f - t) * Mathf.Clamp01(t * 60f) * 0.45f;
            }
            return Create(name, data);
        }

        static AudioClip Noise(string name, float length)
        {
            int count = Mathf.CeilToInt(length * SampleRate);
            var data = new float[count];
            var rng = new System.Random(7);
            float held = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i % 40 == 0) held = (float)rng.NextDouble() * 2f - 1f; // ruído "8 bits"
                float t = i / (float)count;
                data[i] = held * (1f - t) * 0.35f;
            }
            return Create(name, data);
        }

        static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
