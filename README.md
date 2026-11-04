# Sky Jump — Uma Jornada até o Paraíso

Jogo 2D em pixel art feito em Unity 6 (6000.6.8f1, URP 2D), inspirado no estilo "Sky Jump":
uma bola que pula sozinha nas plataformas e sobe por quatro mundos até a vitória.

**Desenvolvedora:** Ana Luiza S. B. de Souza · **Curso:** Ciência da Computação · **Instituição:** UNISAGRADO
**Professor:** Luis Filipe Grael Tinós · **Disciplina:** Desenvolvimento de Jogos Digitais

## Como jogar

1. Abra o projeto no Unity Hub (versão 6000.6.8f1).
2. Abra a cena `Assets/_SkyJump/Scenes/MainMenu.unity` e aperte **Play**.
3. Ou rode o executável gerado em `Builds/Windows/SkyJump.exe` (gerado por *File > Build Profiles*, pasta ignorada pelo git).

| Ação | Teclado | Gamepad | Celular |
|---|---|---|---|
| Mover | A / D ou ← / → | Analógico / D-pad | Segurar lado esquerdo / direito da tela |
| Pausar | Esc ou P | Start | Botão de pausa (canto superior) |

O pulo é automático ao cair sobre uma plataforma.

## Regras

- **Mundos:** Subterrâneo → Terra → Céu → Paraíso → Vitória, com transição suave e dificuldade crescente
  (espaçamento maior, plataformas móveis a partir da Terra, plataformas que quebram no Céu e no Paraíso).
- **Vidas:** 3 vidas para a partida inteira (não recarregam ao trocar de mundo). Ao cair, o jogador reaparece
  numa plataforma de resgate segura; com 0 vidas, tela de derrota.
- **Pontuação:** 10 pontos por unidade de altura máxima alcançada (sem contagem duplicada),
  +250 por mundo novo alcançado e +500 por vida restante na vitória. O recorde fica salvo (PlayerPrefs).
- **Fim:** plataforma de chegada no topo do Paraíso → tela de vitória. Ambas as telas finais mostram a pontuação
  e os botões **Jogar Novamente** e **Voltar ao Início**.

## Áudio

Todo o som é gerado por código no estilo 8 bits, sem arquivos de áudio:

- **Música:** uma trilha em loop para o menu e uma para cada mundo, trocadas com crossfade ao mudar de mundo.
  Fica mais baixa durante a pausa e some com fade na vitória e na derrota.
- **Efeitos:** pulo, quebra de plataforma, perda de vida, troca de mundo, vitória, derrota e cliques.
- Volumes ajustáveis no componente `AudioManager` (`masterVolume`, `musicVolume`).

## Estrutura

```
Assets/_SkyJump/
  Art/          Assets originais (mapas, plataformas, personagem, telas) + sprites de UI gerados
  Data/         WorldData (um por mundo) e material físico
  Prefabs/      Player, Platform
  Scenes/       MainMenu, Game
  Scripts/
    Core/       GameManager (estados), ScoreManager, LifeManager, GoalZone, ScreenInfo
    Player/     PlayerController (movimento, pulo automático, entrada teclado/gamepad/toque)
    Platforms/  Platform (estática, móvel, quebradiça, resgate, chegada), PlatformSpawner (geração + pooling)
    World/      LevelManager (mundos/progresso), CameraController, BackgroundController, WorldData
    UI/         UIManager (HUD e painéis), MainMenuController, fonte pixel, botões, fade, layout responsivo
    Audio/      AudioManager (efeitos e música), ChiptuneComposer (trilhas chiptune geradas por código)
    FX/         Partículas pixeladas
    Debug/      AutoPilot (ferramenta de teste, não usada nas cenas)
  Editor/       SkyJumpBuilder (menu "Sky Jump > Construir Projeto"), SkyJumpCapture (capturas de teste)
```

Estados do jogo: `MainMenu`, `Playing`, `Paused`, `GameOver`, `Victory`.

A geração de plataformas usa a física real da bola (altura máxima do pulo e alcance horizontal),
então toda plataforma seguinte é alcançável.

## Regenerar cenas e prefabs

O menu **Sky Jump > Construir Projeto** recria configurações de importação, sprites de UI, dados dos mundos,
prefabs e as duas cenas. Ele sobrescreve `MainMenu.unity` e `Game.unity`; edições manuais nelas seriam perdidas.

## Responsividade

Funciona em retrato e paisagem sem esticar: a câmera ajusta o zoom para a coluna jogável caber na tela,
a UI usa Canvas Scaler + âncoras + área segura, e os fundos/telas escolhem a arte vertical ou horizontal.
