using System.Collections.Generic;

namespace SkyJump.UI
{
    /// <summary>
    /// Fonte bitmap pixel art 5x7 (maiúsculas, números, pontuação e acentos do português).
    /// Cada glifo ocupa uma grade de 10 linhas: 2 para acentos, 7 para o corpo e 1 para a cedilha.
    /// Não depende de arquivos externos: os glifos são definidos aqui como texto.
    /// </summary>
    public static class PixelFont
    {
        public const int AccentRows = 2;
        public const int BodyRows = 7;
        public const int GlyphRows = AccentRows + BodyRows + 1; // 10
        public const int LetterSpacing = 1;
        public const int SpaceWidth = 3;

        public sealed class Glyph
        {
            public int Width;
            /// <summary>Pixels acesos: [linha, coluna]. Linha 0 = topo da área de acento.</summary>
            public bool[,] Pixels;
        }

        static readonly Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>();
        static Glyph unknown;

        static readonly Dictionary<char, string[]> Body = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", ".#.", ".#.", "###" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            [':'] = new[] { ".", "#", "#", ".", "#", "#", "." },
            ['.'] = new[] { ".", ".", ".", ".", ".", ".", "#" },
            [','] = new[] { "..", "..", "..", "..", "..", ".#", "#." },
            ['!'] = new[] { "#", "#", "#", "#", "#", ".", "#" },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['-'] = new[] { "....", "....", "....", "####", "....", "....", "...." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['('] = new[] { "..#", ".#.", "#..", "#..", "#..", ".#.", "..#" },
            [')'] = new[] { "#..", ".#.", "..#", "..#", "..#", ".#.", "#.." },
            ['\''] = new[] { "#", "#", ".", ".", ".", ".", "." },
            ['<'] = new[] { "...#", "..#.", ".#..", "#...", ".#..", "..#.", "...#" },
            ['>'] = new[] { "#...", ".#..", "..#.", "...#", "..#.", ".#..", "#..." },
            ['='] = new[] { "....", "....", "####", "....", "####", "....", "...." },
            ['*'] = new[] { ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....." },
            ['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
            ['|'] = new[] { "#", "#", "#", "#", "#", "#", "#" },
        };

        // Acentos (2 linhas, alinhados ao centro do glifo de 5 colunas).
        static readonly string[] Acute = { "...#.", "..#.." };
        static readonly string[] Grave = { ".#...", "..#.." };
        static readonly string[] Circumflex = { "..#..", ".#.#." };
        static readonly string[] Tilde = { ".##.#", "#..#." };

        static PixelFont()
        {
            foreach (var pair in Body)
                Glyphs[pair.Key] = Build(pair.Value, null, false);

            AddAccented('Á', 'A', Acute); AddAccented('À', 'A', Grave); AddAccented('Â', 'A', Circumflex); AddAccented('Ã', 'A', Tilde);
            AddAccented('É', 'E', Acute); AddAccented('Ê', 'E', Circumflex); AddAccented('È', 'E', Grave);
            AddAccented('Í', 'I', Acute);
            AddAccented('Ó', 'O', Acute); AddAccented('Ô', 'O', Circumflex); AddAccented('Õ', 'O', Tilde);
            AddAccented('Ú', 'U', Acute);
            Glyphs['Ç'] = Build(Body['C'], null, true);

            unknown = Build(new[] { "#####", "#...#", "#...#", "#...#", "#...#", "#...#", "#####" }, null, false);
        }

        static void AddAccented(char c, char baseChar, string[] accent)
        {
            Glyphs[c] = Build(Body[baseChar], accent, false);
        }

        static Glyph Build(string[] body, string[] accent, bool cedilla)
        {
            int width = body[0].Length;
            var pixels = new bool[GlyphRows, width];
            for (int r = 0; r < BodyRows; r++)
                for (int c = 0; c < width; c++)
                    pixels[AccentRows + r, c] = body[r][c] == '#';

            if (accent != null)
            {
                // Centraliza o acento de 5 colunas em glifos mais estreitos (ex.: I).
                int shift = (accent[0].Length - width) / 2;
                for (int r = 0; r < AccentRows; r++)
                    for (int c = 0; c < accent[r].Length; c++)
                    {
                        int tc = c - shift;
                        if (tc >= 0 && tc < width && accent[r][c] == '#') pixels[r, tc] = true;
                    }
            }

            if (cedilla) pixels[GlyphRows - 1, width / 2] = true;
            return new Glyph { Width = width, Pixels = pixels };
        }

        public static bool IsSpace(char c) => c == ' ';

        public static Glyph Get(char c)
        {
            if (Glyphs.TryGetValue(c, out var g)) return g;
            char upper = char.ToUpperInvariant(c);
            return Glyphs.TryGetValue(upper, out g) ? g : unknown;
        }

        /// <summary>Largura, em pixels da fonte, de uma linha de texto.</summary>
        public static int MeasureLine(string line)
        {
            int w = 0;
            for (int i = 0; i < line.Length; i++)
            {
                w += IsSpace(line[i]) ? SpaceWidth : Get(line[i]).Width;
                if (i < line.Length - 1) w += LetterSpacing;
            }
            return w;
        }
    }
}
