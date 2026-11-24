using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    public enum Sfx { Coin, Build, Cannon, Boom, Splash, Swing, Hit, Chest, Quest, Click, Wood, Hurt, Gull, Thunder }

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
        [Tooltip("Segunda música do mar: alterna com a primeira a cada viagem.")]
        public AudioClip seaMusic2;
        [Tooltip("Toca enquanto houver um navio inimigo.")]
        public AudioClip battleMusic;
        [Tooltip("Ondas ao fundo, em loop, enquanto estiver no mar.")]
        public AudioClip seaAmbience;
        [Range(0, 1)] public float ambienceVolume = 0.25f;
        [Tooltip("Chuva e tempestade no mar (loops).")]
        public AudioClip rainAmbience, stormAmbience;
        [Range(0, 1)] public float weatherVolume = 0.4f;
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

        AudioSource music, musicB, sfx, ambience, weather;
        float weatherTarget;
        bool atSea, inBattle;
        int voyages;
        float fade = 1f;
        readonly AudioSource[] voices = new AudioSource[8];   // sons com tom variado (tiros, explosões)
        int nextVoice;
        readonly Dictionary<Sfx, SfxClip> map = new Dictionary<Sfx, SfxClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();

        void Awake()
        {
            Instance = this;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = musicVolume;
            musicB = gameObject.AddComponent<AudioSource>();
            musicB.loop = true;
            musicB.playOnAwake = false;
            musicB.volume = 0f;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.playOnAwake = false;
            ambience.volume = ambienceVolume;
            weather = gameObject.AddComponent<AudioSource>();
            weather.loop = true;
            weather.playOnAwake = false;
            weather.volume = 0f;
            if (effects != null) foreach (var e in effects) if (e != null && e.clip != null) map[e.id] = e;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            AudioListener.volume = SoundOn ? 1f : 0f;
        }

        void Start() => PlayMusic(false);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Troca a música: true = mar, false = vila.</summary>
        public void PlayMusic(bool sea)
        {
            if (sea && !atSea) voyages++;
            atSea = sea;
            inBattle = false;
            // Ondas ao fundo só no mar
            if (sea && seaAmbience != null && !ambience.isPlaying) { ambience.clip = seaAmbience; ambience.Play(); }
            else if (!sea && ambience.isPlaying) ambience.Stop();
            if (!sea) SetWeatherSound(Pixel.Ship.Weather.Clear);
            CrossFade(CurrentTrack());
        }

        /// <summary>Som de chuva ou tempestade ao fundo (entra e sai devagar).</summary>
        public void SetWeatherSound(Pixel.Ship.Weather w)
        {
            var clip = w == Pixel.Ship.Weather.Storm ? stormAmbience : w == Pixel.Ship.Weather.Rain ? rainAmbience : null;
            weatherTarget = clip != null ? weatherVolume : 0f;
            if (clip != null && weather.clip != clip)
            {
                weather.clip = clip;
                weather.Play();
            }
        }

        /// <summary>Música de combate (true) ou volta para a do mar (false).</summary>
        public void PlayBattle(bool on)
        {
            if (inBattle == on) return;
            inBattle = on;
            CrossFade(CurrentTrack());
        }

        AudioClip CurrentTrack()
        {
            if (!atSea) return villageMusic;
            if (inBattle && battleMusic != null) return battleMusic;
            return seaMusic2 != null && voyages % 2 == 0 ? seaMusic2 : seaMusic;
        }

        /// <summary>Troca de música suavemente (~1,5 s): a atual abaixa enquanto a nova sobe.</summary>
        void CrossFade(AudioClip clip)
        {
            if (clip == null || music.clip == clip) return;
            var old = music;
            music = musicB;
            musicB = old;
            music.clip = clip;
            music.volume = 0f;
            music.Play();
            fade = 0f;
        }

        void Update()
        {
            if (weather != null && weather.volume != weatherTarget)
            {
                weather.volume = Mathf.MoveTowards(weather.volume, weatherTarget, Time.unscaledDeltaTime * weatherVolume / 2.5f);
                if (weather.volume <= 0f && weatherTarget <= 0f) { weather.Stop(); weather.clip = null; }
            }
            if (fade >= 1f) return;
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.5f);
            music.volume = musicVolume * fade;
            musicB.volume = musicVolume * (1f - fade);
            if (fade >= 1f) musicB.Stop();
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

        /// <summary>Toca com o tom um pouco diferente a cada vez (tiros e explosões não soam repetidos).</summary>
        public static void PlayVaried(Sfx id, float volumeScale = 1f, float pitchRange = 0.12f, float basePitch = 1f)
        {
            var a = Instance;
            if (a == null || !a.map.TryGetValue(id, out var e)) return;
            if (a.lastPlayed.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.04f) return;
            a.lastPlayed[id] = Time.unscaledTime;
            var v = a.voices[a.nextVoice];
            a.nextVoice = (a.nextVoice + 1) % a.voices.Length;
            v.pitch = basePitch + UnityEngine.Random.Range(-pitchRange, pitchRange);
            v.PlayOneShot(e.clip, e.volume * volumeScale);
        }
    }
}
