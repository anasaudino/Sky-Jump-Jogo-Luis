using System.Collections.Generic;
using System.IO;
using SkyJump.CameraSystem;
using SkyJump.Core;
using SkyJump.Platforms;
using SkyJump.Player;
using SkyJump.UI;
using SkyJump.World;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyJump.EditorTools
{
    /// <summary>
    /// Monta o projeto inteiro de forma reproduzível: configura a importação dos sprites (pixel art),
    /// gera os elementos de UI em pixel art, cria os dados dos mundos, prefabs, cenas e build settings.
    /// Menu: Sky Jump → Construir Projeto.
    /// </summary>
    public static class SkyJumpBuilder
    {
        const string Root = "Assets/_SkyJump";
        const string Art = Root + "/Art";
        const string UIArt = Art + "/UI";
        const string DataDir = Root + "/Data";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string MenuScenePath = SceneDir + "/MainMenu.unity";
        const string GameScenePath = SceneDir + "/Game.unity";

        static readonly string[] WorldKeys = { "Subterraneo", "Terra", "Ceu", "Paraiso" };

        // Paleta da interface.
        static readonly Color Ink = new Color(0.10f, 0.07f, 0.18f, 1f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.32f, 1f);
        static readonly Color Cream = new Color(1f, 0.97f, 0.88f, 1f);
        const float UIPixel = 1f / 6f; // 1 pixel do sprite de UI = 6 unidades do Canvas

        [MenuItem("Sky Jump/Construir Projeto")]
        public static void BuildAll()
        {
            ConfigureArtImports();
            GenerateUISprites();
            AssetDatabase.Refresh();
            CreateWorldData();
            CreatePhysicsMaterial();
            AssetDatabase.SaveAssets();
            CreatePlayerPrefab(AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>($"{DataDir}/PlayerNoFriction.physicsMaterial2D"));
            CreatePlatformPrefab();
            AssetDatabase.SaveAssets();
            BuildMenuScene();
            BuildGameScene();
            ConfigureProject();
            AssetDatabase.SaveAssets();
            Debug.Log("[Sky Jump] Projeto construído com sucesso.");
        }

        // ------------------------------------------------------------------ Importação

        static void ConfigureArtImports()
        {
            foreach (var w in WorldKeys)
            {
                ImportSprite($"{Art}/Backgrounds/{w}_V.png", 2048, 100, true);
                ImportSprite($"{Art}/Backgrounds/{w}_H.png", 2048, 100, true);
                for (int i = 1; i <= 3; i++) ImportSprite($"{Art}/Platforms/{w}_P{i}.png", 256, 100, true);
            }
            foreach (var s in new[] { "Inicial", "Derrota", "Final" })
            {
                ImportSprite($"{Art}/Screens/{s}_V.png", 2048, 100, true);
                ImportSprite($"{Art}/Screens/{s}_H.png", 2048, 100, true);
            }
            // O personagem (foto) é reduzido para 64 px: vira um sprite pixel art.
            ImportSprite($"{Art}/Character/Personagem.png", 64, 100, false);
        }

        static void ImportSprite(string path, int maxSize, float ppu, bool mipmaps, Vector4? border = null)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogError($"[Sky Jump] Textura não encontrada: {path}"); return; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = mipmaps;
            ti.maxTextureSize = maxSize;
            ti.spritePixelsPerUnit = ppu;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(settings);
            if (border.HasValue) ti.spriteBorder = border.Value;
            ti.SaveAndReimport();
        }

        static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError($"[Sky Jump] Sprite não encontrado: {path}");
            return s;
        }

        /// <summary>Calcula a área não transparente do PNG (normalizada).</summary>
        static Rect VisibleRect(string assetPath)
        {
            var bytes = File.ReadAllBytes(assetPath);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 30)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            Object.DestroyImmediate(tex);
            if (maxX < 0) return new Rect(0, 0, 1, 1);
            return UnityEngine.Rect.MinMaxRect(minX / (float)w, minY / (float)h, (maxX + 1) / (float)w, (maxY + 1) / (float)h);
        }

        // ------------------------------------------------------------------ Sprites de UI gerados

        static void GenerateUISprites()
        {
            Directory.CreateDirectory(UIArt);
            Color O = Ink, H = Color.white, B = new Color(0.87f, 0.87f, 0.87f), S = new Color(0.6f, 0.6f, 0.62f), T = Color.clear;

            // Botão 16x16 com contorno, brilho em cima e sombra embaixo (efeito 3D).
            var button = new Color[16, 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool edge = x == 0 || x == 15 || y == 0 || y == 15;
                    Color c = edge ? O : (y == 14 ? H : (y <= 3 ? S : B));
                    button[x, y] = c;
                }
            foreach (var p in new[] { (0, 0), (15, 0), (0, 15), (15, 15) }) button[p.Item1, p.Item2] = T;
            foreach (var p in new[] { (1, 1), (14, 1), (1, 14), (14, 14) }) button[p.Item1, p.Item2] = O;
            WritePng($"{UIArt}/ui_button.png", button);

            // Painel escuro com moldura dourada.
            Color P = new Color(0.09f, 0.07f, 0.2f, 0.9f), G = Gold, Gd = new Color(0.72f, 0.5f, 0.15f);
            var panel = new Color[16, 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool outer = x == 0 || x == 15 || y == 0 || y == 15;
                    bool ring = x == 1 || x == 14 || y == 1 || y == 14;
                    bool ring2 = x == 2 || x == 13 || y == 2 || y == 13;
                    panel[x, y] = outer ? O : ring ? G : ring2 ? Gd : P;
                }
            foreach (var p in new[] { (0, 0), (15, 0), (0, 15), (15, 15) }) panel[p.Item1, p.Item2] = T;
            WritePng($"{UIArt}/ui_panel.png", panel);

            // Corações 7x7.
            string[] heart =
            {
                ".OO.OO.",
                "OrrOrrO",
                "OrwrrrO",
                "OrrrrrO",
                ".OrrrO.",
                "..OrO..",
                "...O...",
            };
            WritePng($"{UIArt}/heart_full.png", FromPattern(heart, new Dictionary<char, Color>
            {
                ['O'] = O, ['r'] = new Color(0.93f, 0.17f, 0.27f), ['w'] = Color.white
            }));
            WritePng($"{UIArt}/heart_empty.png", FromPattern(heart, new Dictionary<char, Color>
            {
                ['O'] = O, ['r'] = new Color(0.27f, 0.24f, 0.33f), ['w'] = new Color(0.4f, 0.37f, 0.47f)
            }));

            // Pixel branco 4x4 (faixas, barras, flash).
            var pixel = new Color[4, 4];
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) pixel[x, y] = Color.white;
            WritePng($"{UIArt}/pixel.png", pixel);

            // Feixe de luz da chegada (gradiente em degraus, estilo pixel art).
            var beam = new Color[8, 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 8; x++)
                {
                    float a = Mathf.Floor((1f - y / 31f) * 6f) / 6f * 0.8f;
                    float edge = (x == 0 || x == 7) ? 0.5f : 1f;
                    beam[x, y] = new Color(1f, 0.92f, 0.55f, a * edge);
                }
            WritePng($"{UIArt}/goal_beam.png", beam);

            // Sombra do topo do HUD (degradê em degraus) para legibilidade sobre cenários claros.
            var shadeTop = new Color[4, 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 4; x++)
                    shadeTop[x, y] = new Color(0.04f, 0.02f, 0.1f, Mathf.Floor(y / 16f * 5f) / 5f * 0.6f);
            WritePng($"{UIArt}/hud_shade.png", shadeTop);

            AssetDatabase.Refresh();
            ImportSprite($"{UIArt}/ui_button.png", 64, 100, false, new Vector4(3, 4, 3, 3));
            ImportSprite($"{UIArt}/ui_panel.png", 64, 100, false, new Vector4(4, 4, 4, 4));
            ImportSprite($"{UIArt}/heart_full.png", 64, 100, false);
            ImportSprite($"{UIArt}/heart_empty.png", 64, 100, false);
            ImportSprite($"{UIArt}/pixel.png", 64, 4, false);
            ImportSprite($"{UIArt}/goal_beam.png", 64, 8, false);
            ImportSprite($"{UIArt}/hud_shade.png", 64, 100, false);
        }

        static Color[,] FromPattern(string[] rows, Dictionary<char, Color> palette)
        {
            int h = rows.Length, w = rows[0].Length;
            var c = new Color[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[h - 1 - y][x];
                    c[x, y] = palette.TryGetValue(ch, out var col) ? col : Color.clear;
                }
            return c;
        }

        static void WritePng(string path, Color[,] pixels)
        {
            int w = pixels.GetLength(0), h = pixels.GetLength(1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, pixels[x, y]);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ Dados

        static WorldData[] CreateWorldData()
        {
            Directory.CreateDirectory(DataDir);
            var defs = new[]
            {
                new { key = "Subterraneo", name = "SUBTERRÂNEO", color = new Color(0.78f, 0.6f, 1f), length = 55f, width = 2.8f,
                      g0 = new Vector2(1.3f, 1.7f), g1 = new Vector2(1.5f, 2.0f), m0 = 0f, m1 = 0f, speed = new Vector2(0.8f, 1.2f), range = 1.2f, c0 = 0f, c1 = 0f, inset = 0.12f },
                new { key = "Terra", name = "TERRA", color = new Color(0.55f, 1f, 0.45f), length = 65f, width = 2.6f,
                      g0 = new Vector2(1.6f, 2.1f), g1 = new Vector2(1.8f, 2.4f), m0 = 0.25f, m1 = 0.45f, speed = new Vector2(1.0f, 1.7f), range = 1.8f, c0 = 0f, c1 = 0f, inset = 0.16f },
                new { key = "Ceu", name = "CÉU", color = new Color(0.6f, 0.88f, 1f), length = 70f, width = 2.4f,
                      g0 = new Vector2(1.9f, 2.4f), g1 = new Vector2(2.1f, 2.6f), m0 = 0.45f, m1 = 0.6f, speed = new Vector2(1.6f, 2.4f), range = 2.2f, c0 = 0.1f, c1 = 0.18f, inset = 0.22f },
                new { key = "Paraiso", name = "PARAÍSO", color = new Color(1f, 0.86f, 0.4f), length = 75f, width = 2.2f,
                      g0 = new Vector2(2.1f, 2.6f), g1 = new Vector2(2.2f, 2.7f), m0 = 0.5f, m1 = 0.65f, speed = new Vector2(2.0f, 3.0f), range = 2.5f, c0 = 0.18f, c1 = 0.28f, inset = 0.2f },
            };

            var result = new WorldData[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                string path = $"{DataDir}/World{i + 1}_{d.key}.asset";
                var w = AssetDatabase.LoadAssetAtPath<WorldData>(path);
                if (w == null)
                {
                    w = ScriptableObject.CreateInstance<WorldData>();
                    AssetDatabase.CreateAsset(w, path);
                }
                w.displayName = d.name;
                w.themeColor = d.color;
                w.length = d.length;
                w.completionBonus = 250;
                w.backgroundVertical = LoadSprite($"{Art}/Backgrounds/{d.key}_V.png");
                w.backgroundHorizontal = LoadSprite($"{Art}/Backgrounds/{d.key}_H.png");
                w.platformWidth = d.width;
                w.gapStart = d.g0;
                w.gapEnd = d.g1;
                w.movingChanceStart = d.m0;
                w.movingChanceEnd = d.m1;
                w.moveSpeed = d.speed;
                w.maxMoveRange = d.range;
                w.crumbleChanceStart = d.c0;
                w.crumbleChanceEnd = d.c1;
                w.platformSkins = new PlatformSkin[3];
                for (int p = 0; p < 3; p++)
                {
                    string sp = $"{Art}/Platforms/{d.key}_P{p + 1}.png";
                    w.platformSkins[p] = new PlatformSkin { sprite = LoadSprite(sp), visibleRect = VisibleRect(sp), surfaceInset = d.inset };
                }
                EditorUtility.SetDirty(w);
                result[i] = w;
            }
            return result;
        }

        static PhysicsMaterial2D CreatePhysicsMaterial()
        {
            string path = $"{DataDir}/PlayerNoFriction.physicsMaterial2D";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (m == null)
            {
                m = new PhysicsMaterial2D("PlayerNoFriction");
                AssetDatabase.CreateAsset(m, path);
            }
            m.friction = 0f;
            m.bounciness = 0f;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Prefabs

        static GameObject CreatePlayerPrefab(PhysicsMaterial2D mat)
        {
            Directory.CreateDirectory(PrefabDir);
            var root = new GameObject("Player");
            var rb = root.AddComponent<Rigidbody2D>();
            rb.sharedMaterial = mat;
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.sharedMaterial = mat;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite($"{Art}/Character/Personagem.png");
            sr.sortingOrder = 10;
            float scale = 0.95f / sr.sprite.bounds.size.x;
            visual.transform.localScale = new Vector3(scale, scale, 1f);

            var pc = root.AddComponent<PlayerController>();
            new SO(pc).R("visual", visual.transform).R("spriteRenderer", sr).Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Player.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Platform CreatePlatformPrefab()
        {
            var root = new GameObject("Platform");
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var box = root.AddComponent<BoxCollider2D>();
            box.usedByEffector = true;
            var eff = root.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 160f;
            eff.useSideFriction = false;
            eff.useSideBounce = false;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;

            var platform = root.AddComponent<Platform>();
            new SO(platform).R("spriteRenderer", sr).Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Platform.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Platform>();
        }

        // ------------------------------------------------------------------ Cena: Menu

        static void BuildMenuScene()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUICamera();
            CreateEventSystem();
            var canvas = CreateCanvas("MenuCanvas");
            var menu = canvas.gameObject.AddComponent<MainMenuController>();

            var bg = CreateOrientationImage(canvas.transform, "Background", "Inicial");
            var dim = Img(canvas.transform, "Dim", LoadSprite($"{UIArt}/pixel.png"), new Color(0.03f, 0.02f, 0.08f, 0.25f));
            Stretch(dim.rectTransform);

            var safe = Rect(canvas.transform, "SafeArea");
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ----- Grupo principal
            var main = Rect(safe, "Main");
            Stretch(main);
            var mainGroup = main.gameObject.AddComponent<CanvasGroup>();

            var title = Text(main, "Title", "SKY JUMP", 22f, Gold, new Vector2(0, 360), new Vector2(1000, 200), true);
            Text(main, "Subtitle", "UMA JORNADA ATÉ O PARAÍSO", 6f, Cream, new Vector2(0, 245), new Vector2(1000, 70), true);

            var ball = Img(main, "Ball", LoadSprite($"{Art}/Character/Personagem.png"), Color.white);
            ball.preserveAspect = true;
            SetRect(ball.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(110, 110));
            ball.gameObject.AddComponent<UIBounce>();

            var play = Button(main, "PlayButton", "JOGAR", new Color(1f, 0.82f, 0.3f), new Vector2(0, -30), new Vector2(560, 130));
            var credits = Button(main, "CreditsButton", "CRÉDITOS", new Color(0.55f, 0.82f, 1f), new Vector2(0, -185), new Vector2(560, 130));
            var quit = Button(main, "QuitButton", "SAIR", new Color(1f, 0.55f, 0.55f), new Vector2(0, -340), new Vector2(560, 130));
            var best = Text(main, "BestScore", "", 5f, Gold, new Vector2(0, -455), new Vector2(900, 60), true);

            // ----- Créditos
            var creditsRoot = Rect(safe, "CreditsPanel");
            Stretch(creditsRoot);
            var creditsGroup = creditsRoot.gameObject.AddComponent<CanvasGroup>();
            var shade = Img(creditsRoot, "Shade", LoadSprite($"{UIArt}/pixel.png"), new Color(0, 0, 0, 0.55f));
            Stretch(shade.rectTransform);
            var panel = Panel(creditsRoot, "Panel", new Vector2(0, 0), new Vector2(960, 1000));
            Text(panel, "Title", "CRÉDITOS", 9f, Gold, new Vector2(0, 410), new Vector2(880, 110), true);
            string[,] lines =
            {
                { "DESENVOLVEDORA", "ANA LUIZA S. B. DE SOUZA" },
                { "CURSO", "CIÊNCIA DA COMPUTAÇÃO" },
                { "INSTITUIÇÃO", "UNISAGRADO" },
                { "PROFESSOR", "LUIS FILIPE GRAEL TINÓS" },
                { "DISCIPLINA", "DESENVOLVIMENTO DE JOGOS DIGITAIS" },
            };
            for (int i = 0; i < lines.GetLength(0); i++)
            {
                float y = 290 - i * 125;
                Text(panel, $"Label{i}", lines[i, 0], 4f, Gold, new Vector2(0, y), new Vector2(860, 44), false);
                Text(panel, $"Value{i}", lines[i, 1], 5.5f, Cream, new Vector2(0, y - 50), new Vector2(860, 60), false);
            }
            var back = Button(panel, "BackButton", "VOLTAR", new Color(1f, 0.82f, 0.3f), new Vector2(0, -395), new Vector2(460, 120));

            CreateFader(canvas.transform);

            UnityEventTools.AddPersistentListener(play.onClick, new UnityAction(menu.Play));
            UnityEventTools.AddPersistentListener(credits.onClick, new UnityAction(menu.OpenCredits));
            UnityEventTools.AddPersistentListener(quit.onClick, new UnityAction(menu.Quit));
            UnityEventTools.AddPersistentListener(back.onClick, new UnityAction(menu.CloseCredits));

            new SO(menu).R("mainGroup", mainGroup).R("creditsPanel", creditsGroup).R("playButton", play)
                .R("creditsBackButton", back).R("creditsButton", credits).R("bestScoreText", best).Apply();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        // ------------------------------------------------------------------ Cena: Jogo

        static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Carrega os assets DEPOIS de criar a cena: NewScene descarrega objetos não referenciados.
            var worlds = new WorldData[WorldKeys.Length];
            for (int i = 0; i < WorldKeys.Length; i++)
                worlds[i] = AssetDatabase.LoadAssetAtPath<WorldData>($"{DataDir}/World{i + 1}_{WorldKeys[i]}.asset");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Player.prefab");
            var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Platform.prefab").GetComponent<Platform>();
            var pixel = LoadSprite($"{UIArt}/pixel.png");

            // Luz global 2D (URP).
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

            // Jogador.
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerGo.transform.position = new Vector3(0, 1.5f, 0);
            var player = playerGo.GetComponent<PlayerController>();

            // Câmera + cenário + faixas laterais.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 7, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.1f);
            camGo.AddComponent<AudioListener>();
            var camCtrl = camGo.AddComponent<CameraController>();

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(camGo.transform, false);
            var layerA = Sprite(bgGo.transform, "LayerA", null, Color.white, -100);
            var layerB = Sprite(bgGo.transform, "LayerB", null, new Color(1, 1, 1, 0), -99);
            var bgCtrl = bgGo.AddComponent<BackgroundController>();

            var leftBand = Sprite(camGo.transform, "LeftBand", pixel, new Color(0.02f, 0.01f, 0.06f, 0.5f), -50);
            var rightBand = Sprite(camGo.transform, "RightBand", pixel, new Color(0.02f, 0.01f, 0.06f, 0.5f), -50);
            var leftEdge = Sprite(camGo.transform, "LeftEdge", pixel, new Color(1f, 1f, 1f, 0.35f), -49);
            var rightEdge = Sprite(camGo.transform, "RightEdge", pixel, new Color(1f, 1f, 1f, 0.35f), -49);

            // Sistemas.
            var systems = new GameObject("Systems");
            var gm = systems.AddComponent<GameManager>();
            var level = systems.AddComponent<LevelManager>();
            var score = systems.AddComponent<ScoreManager>();
            var lives = systems.AddComponent<LifeManager>();
            var spawnerGo = new GameObject("Platforms");
            var spawner = spawnerGo.AddComponent<PlatformSpawner>();

            // Linha de chegada.
            var goal = new GameObject("Goal");
            var goalCol = goal.AddComponent<BoxCollider2D>();
            goalCol.isTrigger = true;
            goalCol.size = new Vector2(10f, 1.2f);
            goalCol.offset = new Vector2(0f, 0.8f);
            var goalZone = goal.AddComponent<GoalZone>();
            var beam = Sprite(goal.transform, "Glow", LoadSprite($"{UIArt}/goal_beam.png"), new Color(1, 1, 1, 0.85f), -10);
            beam.transform.localScale = new Vector3(7f, 1.6f, 1f);
            beam.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            goal.SetActive(false);

            // UI.
            CreateEventSystem();
            var canvas = CreateCanvas("GameCanvas");
            var ui = canvas.gameObject.AddComponent<UIManager>();
            var flash = Img(canvas.transform, "Flash", pixel, Color.clear);
            Stretch(flash.rectTransform);
            var shade = Img(canvas.transform, "HudShade", LoadSprite($"{UIArt}/hud_shade.png"), Color.white);
            shade.rectTransform.anchorMin = new Vector2(0, 1);
            shade.rectTransform.anchorMax = new Vector2(1, 1);
            shade.rectTransform.pivot = new Vector2(0.5f, 1);
            shade.rectTransform.sizeDelta = new Vector2(0, 300);
            shade.rectTransform.anchoredPosition = Vector2.zero;

            var safe = Rect(canvas.transform, "SafeArea");
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // HUD
            var hudRt = Rect(safe, "HUD");
            Stretch(hudRt);
            var hud = hudRt.gameObject.AddComponent<CanvasGroup>();
            hud.blocksRaycasts = true;

            var heartsRt = Rect(hudRt, "Hearts");
            SetRect(heartsRt, new Vector2(0, 1), new Vector2(30, -28), new Vector2(300, 84), new Vector2(0, 1));
            var hl = heartsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var heartFull = LoadSprite($"{UIArt}/heart_full.png");
            var heartEmpty = LoadSprite($"{UIArt}/heart_empty.png");
            var hearts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                hearts[i] = Img(heartsRt, $"Heart{i + 1}", heartFull, Color.white);
                hearts[i].rectTransform.sizeDelta = new Vector2(84, 84);
                hearts[i].preserveAspect = true;
            }

            var scoreText = Text(hudRt, "Score", "0", 6f, Cream, Vector2.zero, Vector2.zero, true, TextAnchor.UpperRight);
            SetRect(scoreText.rectTransform, new Vector2(1, 1), new Vector2(-150, -30), new Vector2(480, 64), new Vector2(1, 1));
            var scoreCaption = Text(hudRt, "ScoreCaption", "PONTOS", 3f, Gold, Vector2.zero, Vector2.zero, true, TextAnchor.UpperRight);
            SetRect(scoreCaption.rectTransform, new Vector2(1, 1), new Vector2(-150, -102), new Vector2(300, 32), new Vector2(1, 1));

            var pause = Button(hudRt, "PauseButton", "II", new Color(0.95f, 0.95f, 1f), Vector2.zero, Vector2.zero);
            SetRect((RectTransform)pause.transform, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(104, 104), new Vector2(1, 1));

            var worldText = Text(hudRt, "WorldName", "SUBTERRÂNEO", 4f, Cream, Vector2.zero, Vector2.zero, true);
            SetRect(worldText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(620, 48), new Vector2(0.5f, 1));
            var barBg = Img(hudRt, "ProgressBar", LoadSprite($"{UIArt}/ui_button.png"), new Color(0.25f, 0.22f, 0.35f, 0.9f));
            barBg.type = Image.Type.Sliced;
            barBg.pixelsPerUnitMultiplier = 1f / 3f;
            SetRect(barBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -205), new Vector2(620, 36), new Vector2(0.5f, 1));
            var fill = Img(barBg.rectTransform, "Fill", pixel, Gold);
            Stretch(fill.rectTransform, 6);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            for (int i = 1; i < 4; i++)
            {
                var tick = Img(barBg.rectTransform, $"Tick{i}", pixel, Ink);
                float x = TotalFraction(worlds, i);
                tick.rectTransform.anchorMin = new Vector2(x, 0);
                tick.rectTransform.anchorMax = new Vector2(x, 1);
                tick.rectTransform.sizeDelta = new Vector2(6, 0);
                tick.rectTransform.anchoredPosition = Vector2.zero;
            }

            // Aviso de mundo
            var bannerRt = Rect(safe, "WorldBanner");
            SetRect(bannerRt, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1000, 260));
            var banner = bannerRt.gameObject.AddComponent<CanvasGroup>();
            banner.blocksRaycasts = false;
            var bannerTitle = Text(bannerRt, "Title", "MUNDO 1", 7f, Cream, new Vector2(0, 70), new Vector2(1000, 90), true);
            var bannerSub = Text(bannerRt, "Subtitle", "SUBTERRÂNEO", 13f, Gold, new Vector2(0, -50), new Vector2(1000, 150), true);

            // Dica de controles
            var hintRt = Rect(safe, "Hint");
            SetRect(hintRt, new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(940, 170), new Vector2(0.5f, 0));
            var hint = hintRt.gameObject.AddComponent<CanvasGroup>();
            hint.blocksRaycasts = false;
            var hintPanel = Panel(hintRt, "Panel", Vector2.zero, Vector2.zero);
            Stretch(hintPanel);
            var hintText = Text(hintRt, "Text", "", 5f, Cream, Vector2.zero, Vector2.zero, true);
            Stretch(hintText.rectTransform, 30);

            // Setas de toque (mobile)
            var touchRt = Rect(safe, "TouchControls");
            Stretch(touchRt);
            var touch = touchRt.gameObject.AddComponent<CanvasGroup>();
            touch.blocksRaycasts = false;
            touch.alpha = 0f;
            var left = Text(touchRt, "Left", "<", 16f, Color.white, Vector2.zero, Vector2.zero, true);
            SetRect(left.rectTransform, new Vector2(0, 0), new Vector2(60, 60), new Vector2(160, 180), new Vector2(0, 0));
            var right = Text(touchRt, "Right", ">", 16f, Color.white, Vector2.zero, Vector2.zero, true);
            SetRect(right.rectTransform, new Vector2(1, 0), new Vector2(-60, 60), new Vector2(160, 180), new Vector2(1, 0));

            // Pausa
            var pauseRoot = FullPanel(canvas.transform, "PausePanel", null, 0.6f, out var pauseGroup);
            var pausePanel = Panel(pauseRoot, "Panel", Vector2.zero, new Vector2(780, 640));
            Text(pausePanel, "Title", "PAUSADO", 10f, Gold, new Vector2(0, 205), new Vector2(700, 130), true);
            var resume = Button(pausePanel, "ResumeButton", "CONTINUAR", new Color(1f, 0.82f, 0.3f), new Vector2(0, 20), new Vector2(600, 130));
            var pauseMenu = Button(pausePanel, "MenuButton", "VOLTAR AO INÍCIO", new Color(0.55f, 0.82f, 1f), new Vector2(0, -150), new Vector2(600, 130));

            // Derrota
            var overRoot = FullPanel(canvas.transform, "GameOverPanel", "Derrota", 0.3f, out var overGroup);
            var overPanel = Panel(overRoot, "Panel", Vector2.zero, new Vector2(940, 820));
            Text(overPanel, "Title", "FIM DE JOGO", 12f, new Color(1f, 0.4f, 0.35f), new Vector2(0, 280), new Vector2(860, 150), true);
            Text(overPanel, "Message", "SUAS VIDAS ACABARAM", 4.5f, Cream, new Vector2(0, 175), new Vector2(860, 60), true);
            var overScore = Text(overPanel, "Score", "PONTUAÇÃO: 0", 6.5f, Cream, new Vector2(0, 85), new Vector2(860, 80), true);
            var overBest = Text(overPanel, "Best", "RECORDE: 0", 4.5f, Gold, new Vector2(0, 10), new Vector2(860, 56), true);
            var overRetry = Button(overPanel, "RetryButton", "JOGAR NOVAMENTE", new Color(1f, 0.82f, 0.3f), new Vector2(0, -130), new Vector2(680, 130));
            var overMenu = Button(overPanel, "MenuButton", "VOLTAR AO INÍCIO", new Color(0.55f, 0.82f, 1f), new Vector2(0, -290), new Vector2(680, 130));

            // Vitória
            var winRoot = FullPanel(canvas.transform, "VictoryPanel", "Final", 0.15f, out var winGroup);
            var winPanel = Panel(winRoot, "Panel", Vector2.zero, new Vector2(940, 880));
            Text(winPanel, "Title", "VITÓRIA!", 13f, Gold, new Vector2(0, 315), new Vector2(860, 150), true);
            Text(winPanel, "Message", "VOCÊ CHEGOU AO PARAÍSO!", 5f, Cream, new Vector2(0, 205), new Vector2(860, 64), true);
            var winScore = Text(winPanel, "Score", "PONTUAÇÃO: 0", 6.5f, Cream, new Vector2(0, 115), new Vector2(860, 80), true);
            var winDetail = Text(winPanel, "Detail", "VIDAS RESTANTES: 3", 4f, Cream, new Vector2(0, 45), new Vector2(860, 50), true);
            var winBest = Text(winPanel, "Best", "RECORDE: 0", 4.5f, Gold, new Vector2(0, -15), new Vector2(860, 56), true);
            var winRetry = Button(winPanel, "RetryButton", "JOGAR NOVAMENTE", new Color(1f, 0.82f, 0.3f), new Vector2(0, -160), new Vector2(680, 130));
            var winMenu = Button(winPanel, "MenuButton", "VOLTAR AO INÍCIO", new Color(0.55f, 0.82f, 1f), new Vector2(0, -320), new Vector2(680, 130));

            CreateFader(canvas.transform);

            // Botões.
            UnityEventTools.AddPersistentListener(pause.onClick, new UnityAction(gm.TogglePause));
            UnityEventTools.AddPersistentListener(resume.onClick, new UnityAction(gm.TogglePause));
            UnityEventTools.AddPersistentListener(pauseMenu.onClick, new UnityAction(gm.GoToMainMenu));
            UnityEventTools.AddPersistentListener(overRetry.onClick, new UnityAction(gm.RestartGame));
            UnityEventTools.AddPersistentListener(overMenu.onClick, new UnityAction(gm.GoToMainMenu));
            UnityEventTools.AddPersistentListener(winRetry.onClick, new UnityAction(gm.RestartGame));
            UnityEventTools.AddPersistentListener(winMenu.onClick, new UnityAction(gm.GoToMainMenu));

            // Referências.
            new SO(camCtrl).R("player", player).R("leftBand", leftBand).R("rightBand", rightBand)
                .R("leftEdge", leftEdge).R("rightEdge", rightEdge).Apply();
            new SO(bgCtrl).R("cameraController", camCtrl).R("level", level).R("layerA", layerA).R("layerB", layerB).Apply();
            new SO(level).A("worlds", worlds).Apply();
            new SO(spawner).R("platformPrefab", platformPrefab).R("level", level).R("player", player)
                .R("cameraController", camCtrl).R("goalObject", goal).Apply();
            new SO(goalZone).R("gameManager", gm).R("glow", beam.transform).Apply();
            new SO(gm).R("player", player).R("spawner", spawner).R("cameraController", camCtrl).R("level", level)
                .R("score", score).R("lives", lives).R("ui", ui).Apply();
            new SO(ui).R("score", score).R("lives", lives).R("level", level)
                .A("hearts", hearts).R("heartFull", heartFull).R("heartEmpty", heartEmpty)
                .R("scoreText", scoreText).R("worldText", worldText).R("progressFill", fill).R("hud", hud)
                .R("banner", banner).R("bannerTitle", bannerTitle).R("bannerSubtitle", bannerSub)
                .R("hint", hint).R("hintText", hintText).R("touchControls", touch).R("flash", flash)
                .R("pausePanel", pauseGroup).R("pauseFirst", resume)
                .R("gameOverPanel", overGroup).R("gameOverScore", overScore).R("gameOverBest", overBest).R("gameOverFirst", overRetry)
                .R("victoryPanel", winGroup).R("victoryScore", winScore).R("victoryBest", winBest).R("victoryDetail", winDetail).R("victoryFirst", winRetry)
                .Apply();

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        static float TotalFraction(WorldData[] worlds, int upTo)
        {
            float total = 0, partial = 0;
            for (int i = 0; i < worlds.Length; i++)
            {
                total += worlds[i].length;
                if (i < upTo) partial += worlds[i].length;
            }
            return partial / total;
        }

        // ------------------------------------------------------------------ Projeto

        static void ConfigureProject()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorSceneManager.OpenScene(MenuScenePath);
        }

        // ------------------------------------------------------------------ Helpers de cena

        static void CreateUICamera()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();
        }

        static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<ResponsiveCanvas>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void CreateFader(Transform canvas)
        {
            var img = Img(canvas, "Fader", LoadSprite($"{UIArt}/pixel.png"), Color.black);
            Stretch(img.rectTransform);
            img.gameObject.AddComponent<CanvasGroup>();
            img.gameObject.AddComponent<ScreenFader>();
            img.transform.SetAsLastSibling();
        }

        static Image CreateOrientationImage(Transform parent, string name, string screenKey)
        {
            var img = Img(parent, name, LoadSprite($"{Art}/Screens/{screenKey}_V.png"), Color.white);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1080, 1920);
            img.gameObject.AddComponent<AspectRatioFitter>();
            var oi = img.gameObject.AddComponent<OrientationImage>();
            oi.SetSprites(LoadSprite($"{Art}/Screens/{screenKey}_V.png"), LoadSprite($"{Art}/Screens/{screenKey}_H.png"));
            return img;
        }

        /// <summary>Painel de tela cheia (overlay) com fundo opcional e escurecimento.</summary>
        static RectTransform FullPanel(Transform parent, string name, string screenKey, float dimAlpha, out CanvasGroup group)
        {
            var rt = Rect(parent, name);
            Stretch(rt);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            if (screenKey != null) CreateOrientationImage(rt, "Background", screenKey);
            var dim = Img(rt, "Dim", LoadSprite($"{UIArt}/pixel.png"), new Color(0.02f, 0.01f, 0.06f, dimAlpha));
            Stretch(dim.rectTransform);
            var safe = Rect(rt, "SafeArea");
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            rt.gameObject.SetActive(false);
            return safe;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, name, LoadSprite($"{UIArt}/ui_panel.png"), Color.white);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UIPixel;
            img.raycastTarget = true;
            SetRect(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            return img.rectTransform;
        }

        static PixelText Text(Transform parent, string name, string text, float pixelSize, Color color,
                              Vector2 pos, Vector2 size, bool outline, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Rect(parent, name);
            SetRect(rt, new Vector2(0.5f, 0.5f), pos, size);
            var t = rt.gameObject.AddComponent<PixelText>();
            t.Text = text;
            t.color = color;
            t.raycastTarget = false;
            t.Configure(pixelSize, anchor, outline, Ink, true);
            return t;
        }

        static Button Button(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, name, LoadSprite($"{UIArt}/ui_button.png"), color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UIPixel;
            img.raycastTarget = true;
            SetRect(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size);

            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.82f);
            colors.selectedColor = new Color(1f, 1f, 0.82f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var text = Text(img.rectTransform, "Label", label, 6f, Ink, Vector2.zero, Vector2.zero, false);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(28, 30);
            text.rectTransform.offsetMax = new Vector2(-28, -18);
            text.Configure(6f, TextAnchor.MiddleCenter, false, Ink, false);

            var pb = img.gameObject.AddComponent<PixelButton>();
            new SO(pb).R("label", text.rectTransform).Apply();
            return button;
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Atalho para preencher campos privados [SerializeField].</summary>
        sealed class SO
        {
            readonly SerializedObject so;
            public SO(Object target) { so = new SerializedObject(target); }

            SerializedProperty P(string name)
            {
                var p = so.FindProperty(name);
                if (p == null) Debug.LogError($"[Sky Jump] Campo '{name}' não existe em {so.targetObject.GetType().Name}");
                return p;
            }

            public SO R(string name, Object value) { var p = P(name); if (p != null) p.objectReferenceValue = value; return this; }

            public SO A(string name, Object[] values)
            {
                var p = P(name);
                if (p == null) return this;
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                return this;
            }

            public void Apply() => so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
