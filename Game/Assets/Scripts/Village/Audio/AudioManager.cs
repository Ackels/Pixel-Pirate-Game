using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    public enum Sfx { Coin, Build, Cannon, Boom, Splash, Swing, Hit, Chest, Quest, Click }

    /// <summary>
    /// Música (vila / mar) e efeitos sonoros. Chame AudioManager.Play(Sfx.Coin) de qualquer lugar.
    /// Fica na cena da vila (que nunca descarrega), então toca também no mar.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Serializable] public class SfxClip { public Sfx id; public AudioClip clip; [Range(0, 1)] public float volume = 0.7f; }

        public SfxClip[] effects;
        public AudioClip villageMusic, seaMusic;
        [Range(0, 1)] public float musicVolume = 0.35f;

        const string PrefKey = "pixel_sound_on";
        public static bool SoundOn
        {
            get { try { return PlayerPrefs.GetInt(PrefKey, 1) == 1; } catch { return true; } }
            set
            {
                try { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { }
                AudioListener.volume = value ? 1f : 0f;
            }
        }

        AudioSource music, sfx;
        readonly Dictionary<Sfx, SfxClip> map = new Dictionary<Sfx, SfxClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();

        void Awake()
        {
            Instance = this;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = musicVolume;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            if (effects != null) foreach (var e in effects) if (e != null && e.clip != null) map[e.id] = e;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            AudioListener.volume = SoundOn ? 1f : 0f;
        }

        void Start() => PlayMusic(false);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Troca a música: true = mar, false = vila.</summary>
        public void PlayMusic(bool atSea)
        {
            var clip = atSea ? seaMusic : villageMusic;
            if (clip == null || music.clip == clip) return;
            music.clip = clip;
            music.Play();
        }

        public static void Play(Sfx id, float volumeScale = 1f)
        {
            var a = Instance;
            if (a == null || !a.map.TryGetValue(id, out var e)) return;
            // Evita metralhadora de sons iguais no mesmo instante
            if (a.lastPlayed.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.05f) return;
            a.lastPlayed[id] = Time.unscaledTime;
            a.sfx.PlayOneShot(e.clip, e.volume * volumeScale);
        }
    }
}
