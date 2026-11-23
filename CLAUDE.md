# Projeto Pixel — jogo pirata em pixel art

- Jogo novo e original de piratas em pixel art. Mistura mecânicas de **Pixel Piracy** (navio em blocos, tripulação, mar) e **Creator Chronicles** (vila/guilda com construção, ofícios, expedições automáticas). Não copiar nomes, arte ou personagens desses jogos.
- **Fonte da verdade do design:** [docs/GDD.md](docs/GDD.md). Atualize o GDD quando uma decisão de design mudar.
- O usuário é leigo em desenvolvimento de jogos: explique em português simples e recomende em vez de só listar opções.
- Perspectiva (decidida em 23/11/2026): **só a vila é vista de cima** (top-down 3/4). Estaleiro, mar, combate naval e ilhas (desembarque e invasão) são em **visão lateral**, como no Pixel Piracy. A única troca de câmera acontece nas docas da vila.
- Motor planejado: Unity 6 (6000.6.3f1), 2D URP, Pixel Perfect Camera, resolução de referência 640×360, tiles de 16 px, dados em ScriptableObjects.
- **Projeto Unity:** fica em `Game/`. Usa Unity 6000.6.3f1 com o modelo Universal 2D. O pacote `com.unity.pipeline` está instalado, então o Editor pode ser controlado pelo `unity` CLI (`C:/Users/jhona/AppData/Local/Unity/bin/unity.exe`).
  - Passe **sempre** `--project-path C:/Users/jhona/Projetos/Pixel/Game`, porque o Editor do PirateRPG costuma estar aberto ao mesmo tempo.
- **Fase 0 (concluída em 23/11/2026):**
  - Cena `Assets/Scenes/Village.unity`, gerada pelo menu **Pixel > Fase 0 > Montar cena da Vila** (`Assets/Scripts/Editor/Phase0Builder.cs`). Rodar o menu de novo recria a cena, então edições feitas à mão nela se perdem.
  - Arte provisória em `Assets/Art/Placeholder`: sprites gerados por script, 16 PPU, filtro Point.
  - Capitão controlado por `CaptainController` (WASD, setas ou controle); câmera com `CameraFollow` + Pixel Perfect Camera em 640×360.
  - Ordenação top-down por eixo Y, configurada no Renderer2D.
- Outro projeto do usuário, PirateRPG (`C:\Users\jhona\Projetos\PirateRPG`), é **separado**. Não misturar.
