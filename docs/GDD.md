# GDD — Jogo Pirata Pixel (nome provisório: **Enseada dos Corsários**)

> Documento de Design do Jogo (GDD). É o "mapa" do projeto: tudo que vamos construir sai daqui.
> Versão 0.2 — 23/11/2026. Status: **vertical slice jogável (Fases 0–7 concluídas)**.

---

## 1. A ideia em uma frase

**Você é o capitão de um bando pirata: constrói e expande sua vila escondida numa enseada, monta e customiza seu navio bloco a bloco, contrata tripulantes com profissões e personalidades, e sai pelo mar saqueando navios, explorando ilhas e enfrentando masmorras para ficar rico e temido.**

Mistura dos dois jogos de referência:

| Do **Pixel Piracy** pegamos | Do **Creator Chronicles** pegamos |
|---|---|
| Navio construído bloco a bloco (casco, decoração, canhões) | Modo construção da vila (casas, estradas, fazendas, oficinas) |
| Taverna para contratar piratas com atributos e traços | Cada membro tem **casa própria** e uma **profissão** que produz itens |
| Necessidades da tripulação: vida, fome, moral, salário | Produção com **metas** ("quero 10 rum") e venda para visitantes |
| Mapa-múndi com ilhas para visitar | **Crônicas**: missões-guia que ensinam o jogo e dão recompensa |
| Combate em tempo real com abordagem de navios | **Expedições/masmorras**: monta grupos e envia, combate automático |
| Ciclo dia/noite e clima no mar | Criação de personagem (origem, classe, ofício, visual) |
| Escolha de "história do pai" com bônus inicial | Modo difícil com morte permanente / ranking |

O que é **nosso**: tema pirata em tudo, a vila é um **porto pirata** (não uma vila medieval), e as duas metades (vila e mar) se alimentam o tempo todo.

---

## 2. O que eu vi nos vídeos (análise)

### Pixel Piracy (vídeo de 13 min)
- **Visão lateral 2D** (de lado), câmera acompanha o navio; mar com reflexo, céu com sol/lua "com rosto", chuva, noite estrelada.
- Começo: escolhe nome do capitão, semente do mundo, **Modo Lendário**; depois "Como você lembra do seu pai?" → 3 opções que dão bônus (espada, pirata extra ou +25 ouro).
- Ilha inicial com lojas lado a lado: **Taverna**, **Armas**, **Comida**, **Itens**. Interação por "aperte ESPAÇO".
- Taverna: lista de piratas para contratar, cada um com preço.
- Ficha do pirata: nível, XP, vida, fome, moral, **atributos (FOR, VIT, INT, DES, AGI, SORTE)**, uma bio engraçada gerada, item passivo, arma corpo-a-corpo, arma à distância, **grupo** (Abordadores, Artilheiros, Tripulação do navio, Treino…), botão de **pagar salário**.
- **Ship Setup**: construção do navio em blocos — Casco, Decoração, Comida, Diversos, editar bandeira.
- HUD: vida/fome/moral do capitão, ouro, grupos da tripulação no topo, % de vida do navio.
- Navio começa como uma jangada minúscula e vai crescendo.

### Creator Chronicles (vídeo de 27 min)
- **Visão de cima (top-down 3/4)**, arte pixel mais detalhada, interface marrom/madeira.
- Abertura com história narrada (o "desenvolvedor" que entra no próprio jogo).
- Criação de personagem em 6 passos: **Raça** (humano, anão, gnomo, elfo — cada uma com bônus), **Classe** (guerreiro, bruxo, ladino, sacerdote, paladino, mago, caçador, druida), **Profissão** (ex.: alquimia), **Stats**, **Visual** (pele, cabelo, olhos, barba, acessórios), **Dificuldade** (modo "Hard" = morte reduz nível, membros podem ir embora se o humor cair).
- A vila começa **queimada/destruída** — primeira missão é limpar e reconstruir.
- Menu inferior: Construir, Cidade, Renda, Membros, Combate, Alquimia, Culinária, Ferraria, Encantamento.
- Construções: Casa, Estrada, Fazenda, Campos 2x2/3x3/4x4, Lab. de Alquimia… (vários "????" a desbloquear).
- **Cada membro da guilda precisa de uma casa**; cada um tem uma profissão e produz itens.
- **Casa de leilões**: membros põem itens à venda, **visitantes** chegam à vila e compram. Bloquear estradas atrapalha os visitantes.
- **Metas de produção** por item (ex.: 10 de Erva X) — os NPCs trabalham sozinhos para cumprir.
- "Crônica de conquistas": lista de objetivos que funciona como tutorial, cada uma dá recompensa.
- **Masmorras**: você monta "presets" de grupo (até 4 membros + poções) e envia; o combate é **automático**, mostrado numa faixa lateral na parte de baixo da tela; há Masmorra, Raide e PvP.
- Log de eventos ("Fulano comprou X e obteve 20 de ouro"), relógio de dias/horas.

---

## 3. Pesquisa externa (resumo)

**Pixel Piracy** — Quadro Delta, publicado pela Re-Logic, feito em **Unity**. Saiu em 2014; a Enhanced Edition é de 2015. Os consoles tiveram notas baixas (Metacritic 50–53). O objetivo final é derrotar 4 capitães piratas famosos. O capitão tem morte permanente.
- **Os jogadores amaram:** a liberdade de "meu navio, minha tripulação", piratas cheios de personalidade, a arte 16-bit, a trilha de *sea shanties*, a tensão da morte permanente e o sistema fome/moral, simples de entender e punitivo se ignorado.
- **Os jogadores odiaram (NÃO repetir):**
  - controles e combate confusos, com IA caótica;
  - interface confusa e tutorial fraco;
  - fome e moral caindo rápido demais, o que vira grind ("navegar só para pagar a tripulação");
  - traços dos piratas **escondidos até contratar**;
  - repetição no meio do jogo, com ilhas parecidas;
  - construção do navio confusa ou bugada;
  - lentidão com muitos objetos na tela.

**Creator Chronicles** — Origin Studio (Coreia do Sul), Steam, lançado em **14/09/2026**. Avaliações **mistas** (cerca de 66% positivas). O estúdio declarou usar **arte gerada por IA**, o que costuma causar rejeição na Steam.
- **Loop:** limpar as ruínas → construir → recrutar → produzir → vender no leilão ou para visitantes → enviar grupos para masmorras e raides → analisar o **log de batalha** → ajustar e tentar de novo.
- **Ofícios:** alquimia, culinária, ferraria, encantamento, agricultura, mineração e pesca.
- **Derrota:** um membro derrotado pode **largar a guilda**.

**Lições aplicadas neste GDD:**
1. **Traços visíveis**, ou revelados barato, antes de contratar.
2. **Consumo de comida e rum por viagem**, não em tempo real; a cozinha do navio automatiza a manutenção.
3. **Tutorial por capítulos (Crônicas)** que cubra todos os sistemas.
4. **Editor de navio limpo**, com prévia dos atributos (velocidade, carga, poder de fogo) enquanto você constrói.
5. **Ordens claras para a tripulação** e **log de batalha legível**, mostrando por que você perdeu.
6. **Ilhas procedurais misturadas com locais feitos à mão.**
7. **Arte 100% própria**, sem IA generativa na arte final.
8. **Meta-objetivo:** derrotar 5 **Capitães Lendários**, cada um exigindo um tipo diferente de navio e tripulação.

---

## 4. Pilares do jogo (o que nunca pode faltar)

1. **Meu bando, minha história** — cada pirata é único (nome, rosto, traços, bio engraçada) e o jogador se apega a eles.
2. **Construir é recompensador** — tanto o navio quanto a vila crescem visualmente; dá orgulho olhar.
3. **Vila e mar se alimentam** — a vila produz o que o navio precisa (comida, rum, munição, reparos); o mar traz o que a vila precisa (ouro, materiais raros, novos tripulantes, planos de construção).
4. **Fácil de entender, difícil de dominar** — tutorial por missões (Crônicas) em vez de jogar o jogador no escuro.

---

## 5. Loop principal

```
        ┌──────────────── VILA (porto pirata) ─────────────────┐
        │  construir casas/oficinas → tripulantes produzem     │
        │  → vender no Mercado do Porto → ouro                 │
        └───────┬──────────────────────────────────▲───────────┘
                │ comida, rum, munição,            │ ouro, saque, materiais raros,
                │ tripulação, upgrades do navio    │ mapas, novos recrutas, fama
        ┌───────▼──────────────────────────────────┴───────────┐
        │  MAR: mapa-múndi → navegar → eventos/navios inimigos │
        │  → abordagem/combate → ilhas e masmorras → saque     │
        └──────────────────────────────────────────────────────┘
```

**Sessão típica (~20 min):** checar a vila e coletar renda → ajustar metas de produção → construir algo novo → preparar o navio (comida, munição, tripulação) → zarpar → 1–3 encontros no mar + uma ilha → voltar com saque → investir.

---

## 6. Sistemas

### 6.1 Capitão (o seu personagem)
- **Criação**: nome, **Origem** (no lugar de "raça": ex. Marujo do Porto, Ex-Marinha, Náufrago, Filho de Contrabandista — cada uma com bônus), **Estilo de luta** (no lugar de "classe": Duelista, Atirador, Brigão, Curandeiro de bordo, Feiticeiro Vodu, Canhoneiro), **Ofício** inicial, **Visual** (pele, cabelo, barba, chapéu, tapa-olho, perna de pau, cicatrizes, papagaio no ombro) e **Dificuldade**.
- Pergunta-história no início (estilo "como você lembra do seu pai?") que dá um bônus inicial.
- **Equipamento**: arma corpo-a-corpo, arma de fogo, chapéu, casaco, acessório (amuleto), animal de estimação.
  - **Feito (25/11/2026):** arma, chapéu e amuleto para o capitão e cada tripulante, em 4 raridades (Comum, Bom, Raro, Lendário). Armas dão dano (algumas golpeiam mais rápido ou mais longe; a pá serve para cavar o X), chapéus dão vida e amuletos dão bônus (dano, vida, velocidade, mais ouro nos saques). As peças vêm de baús, navios vencidos ou saqueados, expedições, da loja dos portos e da **Forja** da vila. A arma aparece na mão do boneco no mar. Ficam guardadas no Arsenal (tecla I); peças repetidas podem ser vendidas no porto ou no Mercado da vila. Ainda faltam: arma de fogo, casaco e animal de estimação.
- Atributos: Força, Vigor, Destreza, Agilidade, Inteligência, Sorte.
- Se o capitão cair no combate: modo normal = acorda ferido e perde parte do saque; modo difícil = um tripulante leal assume como novo capitão (legado).

### 6.2 Tripulação / Bando
- Contratados na **Taverna** (da vila ou de portos visitados), gerados proceduralmente: nome, visual, bio, atributos, **traços** (ex.: Bêbado, Corajoso, Medroso, Guloso, Supersticioso, Leal). Os traços ficam **visíveis antes de contratar**; um traço raro pode vir oculto, a ser descoberto "pagando uma rodada" de rum.
- **Necessidades**: Fome, Moral, Salário (pago a cada X dias), **Moradia** (cada um precisa de casa na vila **ou** beliche no navio).
- Moral baixa → trabalha pior; muito baixa → **motim** ou vai embora.
- **Funções no navio** (grupos como no Pixel Piracy): Abordadores, Artilheiros, Marujos (reparo/velas), Cozinheiro, Vigia.
- **Ofício na vila** (como no Creator Chronicles) — ver 6.4.
- Sobem de nível em combate (atributos) e trabalhando (nível do ofício).

### 6.3 Vila (Modo Construção)
- Começa como uma **enseada abandonada/queimada** — primeira missão é limpar e reconstruir (gancho do Creator Chronicles).
- Construções (desbloqueio progressivo):
  - **Moradia**: Barraco, Casa, Casarão (mais conforto = mais moral).
  - **Produção**: Horta/Plantação (cana, frutas), Barco de pesca/Pier, **Destilaria de rum**, Cozinha, **Forja/Armeiro**, **Carpintaria naval**, Botica (poções e curativos), Mesa do Cartógrafo.
  - **Social/economia**: Taverna (recrutar + moral), **Mercado do Porto** (venda para visitantes — nosso "leilão"), Armazém, Cofre do tesouro.
  - **Porto**: **Estaleiro** (onde se edita o navio), Docas (mais navios no futuro).
  - **Defesa**: Paliçada, torre de canhão (a vila pode ser atacada pela Marinha — evento).
  - Estradas: visitantes andam por elas; bloquear = menos vendas.
- **Visitantes**: mercadores, contrabandistas e piratas de fora chegam por terra/mar, compram no Mercado e às vezes trazem missões ou viram recrutas.

- **Materiais e decorações (25/11/2026):** além de ouro, construir gasta **Madeira** e **Pedra** (e o Poste de Luz, **Ferro**). Madeira vem da Cabana do Lenhador e de limpar árvores; Pedra da Pedreira e de quebrar rochas; Ferro da Fundição (que derrete pedra). A Forja usa ferro. **Decorações** (postes, fogueira, canteiros, estátua, coluna, estandartes, vasos, caixotes) deixam a vila bonita: cada 2 pontos de beleza dão +1 de moral por dia ao bando (até +10). **Caminhos** de terra (pincel 2×2) podem ser construídos à vontade; em cima deles todo mundo anda 40% mais rápido.
- **A ilha da vila (25/11/2026):** a vila fica numa ilha de verdade, com costa irregular, praia com coqueiros, uma faixa de mata em volta (árvores, bosques, arbustos, pedras, rochedos, tocos) e um campo aberto no meio cheio de flores e capim. Para construir onde há árvore, pedra ou arbusto, é preciso limpar o terreno antes com o Demolir. Casas acendem a luz à noite.

### 6.4 Ofícios e produção
| Ofício | Prédio | Produz | Usado em |
|---|---|---|---|
| Agricultor | Horta/Plantação | Cana, frutas, grãos | Rum, comida |
| Pescador | Pier | Peixe | Comida |
| Cozinheiro | Cozinha | Biscoito do mar, ensopado | Fome no navio |
| Destilador | Destilaria | Rum | Moral, venda (ótimo lucro) |
| Ferreiro/Armeiro | Forja | Espadas, pistolas, balas de canhão | Equipamento, munição |
| Carpinteiro naval | Carpintaria | Tábuas, blocos de casco, canhões | Construir o navio |
| Boticário | Botica | Curativos, poções, antídotos | Combate e masmorras |
| Cartógrafo | Mesa do cartógrafo | Mapas do tesouro, rotas | Desbloqueia ilhas |

- Como no Creator Chronicles: o jogador define **metas** (ex.: "manter 20 rum no estoque") e os tripulantes trabalham sozinhos.
- Nível do item que dá para produzir depende do **nível do ofício** do tripulante.

### 6.5 Navio (Estaleiro)
- Editor em **grade, visão lateral**, bloco a bloco (como o Pixel Piracy): casco, convés, mastros, velas, cabines, porão, beliches, cozinha de bordo, **canhões** (posições), decoração, **bandeira personalizável**.
- Atributos que saem do design: vida do casco, velocidade, capacidade de carga, beliches (= tamanho máximo da tripulação), poder de fogo.
- Começa como **jangada** e cresce até galeão. Blocos são comprados com ouro + materiais da Carpintaria.
- **Foco do jogo (decisão de 24/11/2026): customização do navio.** O jogador deve poder dar forma ao navio com criatividade:
  - **Formas**: bloco cheio, **rampa** (triângulo), **curvo** (quarto de círculo) e **meio bloco**, todos giráveis; casco em várias cores.
  - **Andares**: piso vazado (dá para pular de baixo e descer), **escadas**, paredes de fundo (com janela, com rede/beliche) para porões e cabines.
  - **Velas** que se emendam em qualquer tamanho e com estilos (branca, listrada, preta); bandeira no topo do mastro.
  - **Enfeites**: amurada, lanterna, barril, caixote, âncora, timão (e mais no futuro).
  - Ferramentas do estaleiro: abas por categoria, girar (R), **espelho** (constrói os dois lados), arrastar para pintar/apagar.
  - Próximos passos possíveis: mais formas e cores, enfeites com função (lanterna à noite, timão), bandeira personalizável, salvar/copiar projetos de navio.

### 6.6 Mar e mapa-múndi
- Mapa com ilhas geradas (semente), névoa de guerra, rotas; a vila é a "base".
- **Cada ilha é de um jeito (25/11/2026):** a semente sorteia o que a ilha tem — umas quase vazias, outras com naufrágio guardado por esqueletos, ruínas antigas, ninho de caranguejos, acampamento abandonado, um eremita que conta onde há tesouro; acampamentos piratas com vila, só taverna, tendas ou o forte de um Contramestre; portos com mercado, taverna ou feira. Às vezes há um baú esquecido a mais.
- **Vilas nas ilhas (25/11/2026, como no Pixel Piracy):** acampamentos piratas e portos têm construções de lado (paredes de tijolo, pilares, telhado de telhas e placa com o nome) onde dá para entrar. No acampamento, piratas comem, bebem, conversam ou dormem na Taverna e no Arsenal até verem o bando, e aí partem para a briga. No porto, o comerciante fica no balcão do Mercado e moradores conversam na Taverna.
- Como no Pixel Piracy (24/11/2026): ilhas desconhecidas perto de onde você já esteve aparecem como "?" e dá para navegar até elas para descobrir; cada ilha mostra o nível de perigo, e passar o mouse mostra tipo, perigo e distância.
- Viagem gasta dias → consome comida e rum **por trecho de viagem** (dá para planejar antes de zarpar, sem micro-gestão); clima (sol, chuva, tempestade) e dia/noite.
- **Encontros**: navios mercantes (saque fácil), Marinha (perigoso), piratas rivais, eventos (náufrago, garrafa com mapa, kraken…).
- **Ilhas**: portos amigáveis (lojas, taverna), ilhas selvagens (coleta), fortes (combate), **masmorras** (cavernas, naufrágios, templos).

### 6.7 Combate
Dois modos, para agradar os dois estilos:
1. **Combate ativo**: tempo real, sempre na **visão lateral**: navio contra navio, abordagem e invasão de ilhas e fortes (a tripulação desembarca na praia e luta). O jogador controla o **capitão** diretamente; a tripulação segue ordens por grupo (Abordadores atacam, Artilheiros nos canhões, Marujos reparam). Navio vs navio: troca de tiros → abordagem.
   - **Combate por ordens (decidido em 24/11/2026, como no Pixel Piracy):** o navio inimigo para bem longe, um de cada lado (a câmera afasta para mostrar os dois), já com a tripulação dele à vista, e nada acontece do seu lado até você mandar: **Atirar / Cessar fogo** (canhões), **Abordar** (a qualquer hora: cada um lança o próprio gancho e atravessa andando pela corda) e **Fugir** (ou **Seguir viagem**, se ninguém atirou ainda). Abordar com muitos inimigos no convés é arriscado: os canhões servem para derrubar tripulantes deles antes. Piratas abrem fogo sozinhos depois de alguns segundos e abordam você com ganchos; mercantes só revidam depois do seu primeiro tiro.
   - **Controle direto (24/11/2026):** clicar num inimigo faz o capitão ir até ele (na corda, se estiver no outro navio) e atacar. **Pausa tática (P):** o jogo para e você arrasta a espada de cada tripulante até um inimigo (atacar) ou um lugar (ir), com uma linha em arco mostrando a ordem; também dá para arrastar o tripulante sem pausar. **Barra de comandos** (canto de baixo): Mover e Atacar (arrastar, para a tripulação inteira), Interagir, Reunir e Cavar (clique), cada um com a explicação no mouse; painel do personagem selecionado com vida, fome e moral. **Tesouro enterrado:** nas ilhas do mapa do tesouro, o baú fica embaixo de um X na areia e precisa ser cavado. **Tela de decisão:** quando um navio aparece (ou acontece um evento no mar), a tela escurece e o capitão escolhe — enfrentar ou fugir (com a chance em %), resgatar o náufrago, contornar a tempestade... **Depois da rendição:** Capturar o navio (vira o seu; o antigo é vendido), Saquear (com todos a bordo: carga com chance de cada mercadoria, e o navio deles explode) ou Deixar ir.
2. **Expedições automáticas** (estilo Creator Chronicles): monta "presets" de grupo (até 4–5 tripulantes + poções) e envia para uma masmorra/saque **sem o capitão**; combate automático mostrado numa faixa na tela, enquanto você cuida da vila. Volta com saque, XP — ou feridos. Sempre com **log de batalha** explicando o resultado.
3. **Derrota pesada** pode fazer um tripulante **desertar** (pode ser reencontrado depois em alguma ilha) — no modo Lendário, pode morrer.

### 6.10 Objetivo final
Derrotar os **5 Capitães Lendários** que dominam os mares — cada um em uma região, exigindo uma estratégia diferente (navio rápido, navio blindado, tripulação de abordagem, artilharia, expedição em masmorra).
- **Feito (25/11/2026): o 1º, Capitão Corvo Rubro**, na Fortaleza do Corvo (leste do mapa, perigo 5). Ela aparece explorando ou por um boato depois de 3 vitórias no mar. Canhões na praia atiram no bando; 4 guardas; o chefe dorme no trono até os guardas caírem (ou alguém chegar perto). Ele é grande, dá uma pancada no chão que acerta todo mundo em volta (avisada), chama reforços com 2/3 e 1/3 da vida e fica furioso no fim. Barra de vida grande no topo. Prêmio: muito ouro, uma peça lendária garantida e mais uma rara; crônica "O Corvo Rubro". Faltam os outros 4 (cada um com um desafio diferente).

### 6.8 Economia e progressão
- Moedas: **Ouro** (principal), **Dobrões** (raros, para coisas especiais), **Infâmia** (reputação pirata: desbloqueia recrutas melhores, ilhas, construções).
- Facções: Marinha, Companhia Mercante, Irmandade Pirata, Nativos das ilhas — reputação afeta preços e ataques.
- **Crônicas** (missões-guia): sequência de objetivos que ensina o jogo e dá recompensas (ex.: "Construa uma casa", "Contrate 2 piratas", "Venda 5 rum no Mercado", "Afunde um navio mercante").

### 6.9 Dificuldade
- **Normal**: derrotas custam ouro/saque.
- **Lendário**: morte permanente da tripulação, moral mais sensível, motins; ranking de tempo para terminar.

---

## 7. Visual e áudio

- **Pixel art**. Resolução de referência **640×360** (escala perfeita para 1080p ×3 e 4K ×6).
  - Vila (25/11/2026): a câmera usa zoom inteiro e mostra ~16 blocos de altura em qualquer janela; a rodinha do mouse aproxima ou afasta.
  - Mar e ilhas (25/11/2026): personagens com 1,5 bloco de altura (antes 1), como no Pixel Piracy, onde as pessoas são mais altas que os blocos do navio.
- Personagens 16×16 px (estilo "chibi" do Ninja Adventure), tiles de 16×16 px na vila.
- **Arte (decidido em 23/11/2026): packs CC0 do itch.io**, todos de uso comercial livre e sem crédito obrigatório:
  - **Ninja Adventure** (Pixel-boy): vila de cima e **todos os personagens** (capitão, tripulantes, visitantes, esqueletos).
  - **Treasure Hunters** (Pixel Frog): cenário do mar e das ilhas (blocos de 32 px), baús, inimigos-bicho (caranguejo, estrela-do-mar).
  - **Pirate Bomb** (Pixel Frog): reserva. Os piratas dele são grandes demais para a tripulação; talvez sirvam como chefões.
  - Peças que faltam (coqueiro da vila, blocos de casco do navio) são montadas ou desenhadas no mesmo estilo e com a paleta dos packs.
- **Mesmo personagem nos dois mundos:** cada tripulante tem um **visual** (um personagem do Ninja Adventure), guardado no save. Na vila ele aparece de cima; no mar, usa os quadros de perfil (esquerda/direita) do mesmo personagem. No mar, os personagens aparecem com o pixel 2× maior que o do cenário — aceito pelo usuário em troca de serem os mesmos.
- Paleta quente e tropical (mar turquesa, areia, madeira, pôr-do-sol), com noite azul-roxa.
- Efeitos: reflexo na água (imagem espelhada ondulando), ondas, chuva, ciclo dia/noite, luzes de fogueira, velas e lanternas que brilham à noite, brilho suave (bloom), vinheta e fundos desfocados para dar profundidade (decidido em 25/11/2026: pixel art, mas "bonita" como o Pixel Piracy, sem o visual de pixelão).
- Interface: madeira + pergaminho + corda (tema pirata).
- Música: shanty/folk instrumental, **orquestrada, nada de 8-bit** (decidido em 24/11/2026): vila, duas do mar que se alternam a cada viagem e uma de combate, com troca suave entre elas; sons de ondas, gaivotas, canhões, chuva e trovões.
- **Céu do mar (24/11/2026):** muda aos poucos com o relógio do dia — amanhecer, dia azul, tarde dourada, pôr do sol, anoitecer e noite com lua e estrelas; o sol e a lua cruzam o céu. **Clima:** chuva (~25% dos trechos) e tempestade (~10%, e no evento "tempestade"), com céu e nuvens cinza, luz mais fraca, relâmpagos e trovões.

### Câmera / perspectiva — **DECIDIDO (23/11/2026): "a vila de cima, a aventura de lado"**
| Onde | Câmera | Por quê |
|---|---|---|
| **Vila** (construção, produção, visitantes) | **De cima (top-down 3/4)** — a única parte de cima | Liberdade para planejar ruas, hortas e prédios, como no Creator Chronicles |
| **Estaleiro** (editor do navio) | **De lado** | O navio em blocos visto de perfil é a identidade do gênero |
| **Mar** (navegação, navio contra navio, abordagem) | **De lado** | Canhões, ondas e abordagem ficam mais claros de lado |
| **Ilhas** (desembarque, invasão, exploração, combate em terra) | **De lado**, como no Pixel Piracy | O navio ancora e a tripulação desce para a praia no mesmo plano lateral, sem corte de câmera |
| **Expedições automáticas** | Faixa lateral na tela | Assiste enquanto cuida da vila |

Na prática, são **dois mundos**: a **vila** (gestão, de cima) e a **aventura** (navio + mar + ilhas, de lado). Tudo que é aventura compartilha a mesma câmera, a mesma física e o mesmo sistema de combate.

**Regras para a mistura funcionar:**
- **Mesmos personagens nas duas visões.** Cada tripulante tem sprites de 4 direções (para cima) e de perfil (para o lado). O perfil top-down "de lado" deve ser reaproveitado na visão lateral sempre que der.
- **Mesma paleta de cores** em tudo. O mar usa blocos de 32 px (Treasure Hunters) com os personagens de 16 px desenhados em dobro.
- **Uma única transição:** ao embarcar nas docas da vila, a tela corta para a visão lateral do navio. Ao voltar para casa e atracar, ela volta para a visão de cima. Dentro da aventura não há mais cortes: navio, mar e ilhas são contínuos.

**Custo:** são dois conjuntos de cenário e dois sistemas de movimento (de cima e lateral). Como as ilhas também são laterais, todo o combate fica num lugar só, a visão lateral. A vila não precisa de combate no MVP. O MVP começa pela **vila de cima** (Fases 0–2), e depois vem a aventura lateral (Fases 3+).

---

## 8. Plataforma e tecnologia

- **PC (Windows) via Steam** como alvo inicial. Controles: mouse + teclado (controle depois).
- **Motor: Unity 6** (versão 6000.6.3f1, a mesma já instalada e usada no PirateRPG), modo **2D com URP** e **Pixel Perfect Camera**.
  - Por quê: você já tem o Unity instalado e um projeto nele, há muito material de aprendizado, e eu tenho ferramentas específicas para Unity (tiles, sprites, pixel perfect).
- Dados do jogo (itens, construções, ofícios, traços) em **ScriptableObjects** — fácil de balancear sem mexer em código.
- Arte: **Aseprite** para pixel art; no início podemos usar **placeholders** (quadrados coloridos) ou packs de assets gratuitos/comprados (itch.io) para testar a jogabilidade antes de investir em arte final.

---

## 9. Escopo — o que entra primeiro (MVP)

Não dá para fazer tudo de uma vez. Ordem proposta:

| Fase | Entrega | O que dá pra jogar |
|---|---|---|
| **0. Protótipo de base** ✅ | Projeto Unity, câmera pixel perfect, capitão andando em 4/8 direções (visão de cima), tilemap da enseada | Andar pela vila |
| **1. Vila mínima** ✅ | Modo construção (casa, horta, destilaria, mercado), 1 recurso → 1 produto, visitantes compram | Construir e ganhar ouro |
| **2. Tripulação** ✅ | Taverna, recrutas gerados, fome/moral/salário, ofícios com metas | Contratar e ver a vila trabalhar sozinha |
| **3. Navio** ✅ | Estaleiro em grade (lateral), blocos básicos, embarcar nas docas → corte para o mar lateral | Montar a jangada e sair |
| **4. Combate** ✅ | Capitão luta, navio inimigo, canhões, abordagem, saque | Primeiro saque! |
| **5. Mapa-múndi** ✅ | Ilhas (desembarque lateral), viagem com consumo, eventos | Exploração e invasão de ilhas |
| **6. Expedições** ✅ | Presets de grupo, masmorra com combate automático | Loop completo |
| **7. Crônicas + polimento** ✅ | Tutorial por missões, save/load, menu, sons | **Vertical slice** apresentável |

Depois: mais construções, facções, ataques à vila, chefes, modo Lendário, Steam.

---

## 10. Riscos

- **Escopo grande** para uma pessoa → seguir as fases à risca; cada fase precisa ser jogável.
- **Duas perspectivas** (de cima + lateral) aumentam a arte e o código → mesmos personagens e mesma paleta, e construir uma de cada vez.
- **Arte** é o maior gargalo → começar com placeholders/packs; decidir cedo se vai ter artista.
- **IA da tripulação** (pathfinding em navio e vila) é complexa → simplificar no MVP (tarefas por fila, caminhos em linha no plano lateral).

---

## 11. Decisões em aberto

1. ~~Perspectiva~~ → **decidido: só a vila de cima; navio, mar e ilhas de lado** (seção 7).
2. Nome do jogo.
3. ~~Arte~~ → **decidido: packs CC0 (Ninja Adventure + Treasure Hunters)**, com peças complementares no mesmo estilo (seção 7).
4. Tom: mais cômico (como Pixel Piracy) ou mais aventura séria?

---

## 12. Glossário rápido (para quem está começando)
- **GDD**: este documento — a "bíblia" do jogo.
- **Core loop**: o ciclo de ações que o jogador repete e que deixa o jogo viciante.
- **MVP / Vertical slice**: a menor versão jogável que já mostra o jogo "de verdade".
- **Placeholder**: arte provisória só para testar.
- **Tilemap**: grade de quadradinhos usada para montar cenários.
- **ScriptableObject**: arquivo de dados do Unity (ex.: "Espada de ferro: dano 5, preço 20").
