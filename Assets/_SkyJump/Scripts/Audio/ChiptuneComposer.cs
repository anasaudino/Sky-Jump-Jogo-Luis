using UnityEngine;

namespace SkyJump.Audio
{
    /// <summary>Trilhas disponíveis: uma para o menu e uma para cada mundo.</summary>
    public enum Music { None, Menu, Subterraneo, Terra, Ceu, Paraiso }

    /// <summary>
    /// Compõe e sintetiza músicas chiptune (8 bits) em loop, sem arquivos de áudio.
    /// Cada trilha tem 8 compassos: melodia (onda quadrada), baixo (onda triangular),
    /// arpejo opcional e bateria de ruído. A melodia é gerada a partir das notas dos acordes
    /// com semente fixa, então a música é sempre a mesma a cada execução.
    /// </summary>
    public static class ChiptuneComposer
    {
        public const int SampleRate = 22050;

        const int Bars = 8;
        const int StepsPerBar = 8; // colcheias
        const int Rest = -1000;
        const int Hold = -999;

        static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };

        // 1 = nova nota, 0 = sustenta a anterior, 2 = pausa.
        static readonly int[][] Rhythms =
        {
            new[] { 1, 0, 1, 0, 1, 1, 1, 0 },
            new[] { 1, 1, 1, 0, 1, 0, 1, 0 },
            new[] { 1, 0, 0, 1, 0, 0, 1, 0 },
            new[] { 1, 0, 1, 1, 1, 0, 2, 0 },
            new[] { 1, 1, 1, 1, 1, 0, 1, 0 },
        };
        static readonly int[] EndingRhythm = { 1, 0, 1, 0, 1, 0, 0, 0 };

        class Song
        {
            public string name;
            public int bpm;
            public int root;      // nota MIDI da tônica da melodia
            public bool minor;
            public int[] chords;  // graus da escala (0 = I), um por compasso, repetidos
            public float duty;    // timbre da melodia
            public int seed;
            public bool arp;
            public bool snare;
        }

        static Song Get(Music m)
        {
            switch (m)
            {
                case Music.Menu:        return new Song { name = "music_menu",        bpm = 108, root = 72, minor = false, chords = new[] { 0, 4, 5, 3 }, duty = 0.25f,  seed = 11, arp = false, snare = false };
                case Music.Subterraneo: return new Song { name = "music_subterraneo", bpm = 100, root = 69, minor = true,  chords = new[] { 0, 5, 2, 6 }, duty = 0.125f, seed = 23, arp = true,  snare = false };
                case Music.Terra:       return new Song { name = "music_terra",       bpm = 118, root = 67, minor = false, chords = new[] { 0, 3, 4, 0 }, duty = 0.25f,  seed = 37, arp = false, snare = true };
                case Music.Ceu:         return new Song { name = "music_ceu",         bpm = 126, root = 74, minor = false, chords = new[] { 0, 5, 3, 4 }, duty = 0.5f,   seed = 41, arp = true,  snare = true };
                case Music.Paraiso:     return new Song { name = "music_paraiso",     bpm = 136, root = 76, minor = false, chords = new[] { 3, 4, 2, 5 }, duty = 0.25f,  seed = 53, arp = true,  snare = true };
                default: return null;
            }
        }

        public static AudioClip Build(Music music)
        {
            var song = Get(music);
            if (song == null) return null;

            int[] scale = song.minor ? Minor : Major;
            int step = Mathf.RoundToInt(60f / song.bpm / 2f * SampleRate);
            int total = step * Bars * StepsPerBar;
            var buf = new float[total];
            var rng = new System.Random(song.seed);

            // Melodia
            int[] melody = ComposeMelody(song, rng);
            for (int i = 0; i < melody.Length;)
            {
                if (melody[i] == Rest || melody[i] == Hold) { i++; continue; }
                int len = 1;
                while (i + len < melody.Length && melody[i + len] == Hold) len++;
                float f = Freq(song.root + Semi(scale, melody[i]));
                Square(buf, i * step, len * step, f, song.duty, 0.16f, len >= 2);
                i += len;
            }

            for (int bar = 0; bar < Bars; bar++)
            {
                int chord = song.chords[bar % song.chords.Length];
                int barStart = bar * StepsPerBar * step;

                // Baixo em semínimas: tônica, oitava, quinta, oitava.
                int[] bassDeg = { chord, chord + 7, chord + 4, chord + 7 };
                for (int q = 0; q < 4; q++)
                    Triangle(buf, barStart + q * 2 * step, (int)(2 * step * 0.85f), Freq(song.root - 24 + Semi(scale, bassDeg[q])), 0.24f);

                // Arpejo em semicolcheias.
                if (song.arp)
                {
                    int[] arpDeg = { chord, chord + 2, chord + 4, chord + 7 };
                    int half = step / 2;
                    for (int k = 0; k < StepsPerBar * 2; k++)
                        Square(buf, barStart + k * half, (int)(half * 0.8f), Freq(song.root - 12 + Semi(scale, arpDeg[k % 4])), 0.5f, 0.045f, false);
                }

                // Bateria.
                for (int s = 0; s < StepsPerBar; s++)
                {
                    int at = barStart + s * step;
                    if (s == 0 || s == 4 || (song.snare && s == 5 && bar % 2 == 1)) Kick(buf, at, 0.32f);
                    if (song.snare && (s == 2 || s == 6)) Snare(buf, at, 0.11f, rng);
                    if (s % 2 == 1 || song.snare) Hat(buf, at, s % 2 == 1 ? 0.05f : 0.03f, rng);
                }
            }

            Normalize(buf, 0.8f);
            var clip = AudioClip.Create(song.name, total, 1, SampleRate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        // ---------------- Composição ----------------

        static int[] ComposeMelody(Song song, System.Random rng)
        {
            var mel = new int[Bars * StepsPerBar];
            int cur = 2;
            for (int bar = 0; bar < 4; bar++) cur = ComposeBar(mel, bar, song, rng, cur, Rhythms[rng.Next(Rhythms.Length)]);
            // Segunda frase: repete o início (mesmos acordes) e cria um final novo.
            System.Array.Copy(mel, 0, mel, 4 * StepsPerBar, 2 * StepsPerBar);
            cur = ComposeBar(mel, 6, song, rng, cur, Rhythms[rng.Next(Rhythms.Length)]);
            ComposeBar(mel, 7, song, rng, cur, EndingRhythm);
            return mel;
        }

        static int ComposeBar(int[] mel, int bar, Song song, System.Random rng, int cur, int[] rhythm)
        {
            int chord = song.chords[bar % song.chords.Length];
            for (int s = 0; s < StepsPerBar; s++)
            {
                int idx = bar * StepsPerBar + s;
                if (rhythm[s] == 0) { mel[idx] = Hold; continue; }
                if (rhythm[s] == 2) { mel[idx] = Rest; continue; }

                bool strong = s % 4 == 0;
                if (strong || rng.NextDouble() < 0.6)
                    cur = NearestChordTone(chord, cur + rng.Next(-2, 3));
                else
                    cur += rng.NextDouble() < 0.5 ? -1 : 1; // nota de passagem
                cur = Mathf.Clamp(cur, -1, 9);
                mel[idx] = cur;
            }
            return cur;
        }

        static int NearestChordTone(int chord, int target)
        {
            int best = chord, bestDist = int.MaxValue;
            for (int oct = -7; oct <= 14; oct += 7)
                for (int k = 0; k <= 4; k += 2)
                {
                    int d = chord + k + oct;
                    if (d < -1 || d > 9) continue;
                    int dist = Mathf.Abs(d - target);
                    if (dist < bestDist) { bestDist = dist; best = d; }
                }
            return best;
        }

        static int Semi(int[] scale, int degree)
        {
            int oct = Mathf.FloorToInt(degree / 7f);
            int i = degree - oct * 7;
            return oct * 12 + scale[i];
        }

        static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        // ---------------- Síntese ----------------

        static void Square(float[] buf, int start, int len, float freq, float duty, float vol, bool vibrato)
        {
            float phase = 0f;
            int attack = (int)(0.004f * SampleRate), release = (int)(0.03f * SampleRate);
            for (int k = 0; k < len; k++)
            {
                int i = start + k;
                if (i >= buf.Length) break;
                float t = k / (float)SampleRate;
                float env = Mathf.Min(1f, k / (float)attack) * Mathf.Min(1f, (len - k) / (float)release) * (0.65f + 0.35f * Mathf.Exp(-t * 8f));
                float f = vibrato ? freq * (1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01((t - 0.12f) * 5f)) : freq;
                phase += f / SampleRate;
                phase -= Mathf.Floor(phase);
                buf[i] += (phase < duty ? 1f : -1f) * vol * env;
            }
        }

        static void Triangle(float[] buf, int start, int len, float freq, float vol)
        {
            float phase = 0f;
            int release = (int)(0.02f * SampleRate);
            for (int k = 0; k < len; k++)
            {
                int i = start + k;
                if (i >= buf.Length) break;
                phase += freq / SampleRate;
                phase -= Mathf.Floor(phase);
                float env = Mathf.Min(1f, k / 40f) * Mathf.Min(1f, (len - k) / (float)release);
                buf[i] += (4f * Mathf.Abs(phase - 0.5f) - 1f) * vol * env;
            }
        }

        static void Kick(float[] buf, int start, float vol)
        {
            int len = (int)(0.14f * SampleRate);
            float phase = 0f;
            for (int k = 0; k < len; k++)
            {
                int i = start + k;
                if (i >= buf.Length) break;
                float t = k / (float)SampleRate;
                phase += Mathf.Lerp(45f, 150f, Mathf.Exp(-t * 30f)) / SampleRate;
                buf[i] += Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 18f) * vol;
            }
        }

        static void Snare(float[] buf, int start, float vol, System.Random rng)
        {
            int len = (int)(0.12f * SampleRate);
            float held = 0f;
            for (int k = 0; k < len; k++)
            {
                int i = start + k;
                if (i >= buf.Length) break;
                if (k % 2 == 0) held = (float)rng.NextDouble() * 2f - 1f;
                float t = k / (float)SampleRate;
                buf[i] += held * Mathf.Exp(-t * 25f) * vol;
            }
        }

        static void Hat(float[] buf, int start, float vol, System.Random rng)
        {
            int len = (int)(0.04f * SampleRate);
            for (int k = 0; k < len; k++)
            {
                int i = start + k;
                if (i >= buf.Length) break;
                float t = k / (float)SampleRate;
                buf[i] += ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 90f) * vol;
            }
        }

        static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max <= 0f) return;
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }
    }
}
