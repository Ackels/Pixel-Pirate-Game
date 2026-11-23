# GDD — Jogo Pirata Pixel (nome provisório: **Enseada dos Corsários**)

> Documento de Design do Jogo (GDD). É o "mapa" do projeto: tudo que vamos construir sai daqui.
> Versão 0.1 — 23/11/2026. Status: **rascunho para aprovação**.

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

### 6.6 Mar e mapa-múndi
- Mapa com ilhas geradas (semente), névoa de guerra, rotas; a vila é a "base".
- Viagem gasta dias → consome comida e rum **por trecho de viagem** (dá para planejar antes de zarpar, sem micro-gestão); clima (sol, chuva, tempestade) e dia/noite.
- **Encontros**: navios mercantes (saque fácil), Marinha (perigoso), piratas rivais, eventos (náufrago, garrafa com mapa, kraken…).
- **Ilhas**: portos amigáveis (lojas, taverna), ilhas selvagens (coleta), fortes (combate), **masmorras** (cavernas, naufrágios, templos).

### 6.7 Combate
Dois modos, para agradar os dois estilos:
1. **Combate ativo**: tempo real, sempre na **visão lateral**: navio contra navio, abordagem e invasão de ilhas e fortes (a tripulação desembarca na praia e luta). O jogador controla o **capitão** diretamente; a tripulação segue ordens por grupo (Abordadores atacam, Artilheiros nos canhões, Marujos reparam). Navio vs navio: troca de tiros → abordagem.
2. **Expedições automáticas** (estilo Creator Chronicles): monta "presets" de grupo (até 4–5 tripulantes + poções) e envia para uma masmorra/saque **sem o capitão**; combate automático mostrado numa faixa na tela, enquanto você cuida da vila. Volta com saque, XP — ou feridos. Sempre com **log de batalha** explicando o resultado.
3. **Derrota pesada** pode fazer um tripulante **desertar** (pode ser reencontrado depois em alguma ilha) — no modo Lendário, pode morrer.

### 6.10 Objetivo final
Derrotar os **5 Capitães Lendários** que dominam os mares — cada um em uma região, exigindo uma estratégia diferente (navio rápido, navio blindado, tripulação de abordagem, artilharia, expedição em masmorra).

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
- Personagens ~16×24 px, blocos/tiles de 16×16 px.
- Paleta quente e tropical (mar turquesa, areia, madeira, pôr-do-sol), com noite azul-roxa.
- Efeitos: reflexo na água, ondas, chuva, ciclo dia/noite, luz de lampiões à noite.
- Interface: madeira + pergaminho + corda (tema pirata).
- Música: shanty/folk instrumental; sons de ondas, gaivotas, canhões.

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
- **Mesma paleta de cores e mesmo tamanho de pixel** (16 px) em tudo, para parecer um jogo só.
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
| **2. Tripulação** | Taverna, recrutas gerados, fome/moral/salário, ofícios com metas | Contratar e ver a vila trabalhar sozinha |
| **3. Navio** | Estaleiro em grade (lateral), blocos básicos, embarcar nas docas → corte para o mar lateral | Montar a jangada e sair |
| **4. Combate** | Capitão luta, navio inimigo, canhões, abordagem, saque | Primeiro saque! |
| **5. Mapa-múndi** | Ilhas (desembarque lateral), viagem com consumo, eventos | Exploração e invasão de ilhas |
| **6. Expedições** | Presets de grupo, masmorra com combate automático | Loop completo |
| **7. Crônicas + polimento** | Tutorial por missões, save/load, menu, sons | **Vertical slice** apresentável |

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
3. Arte: placeholders + pack pronto no início, ou artista desde já?
4. Tom: mais cômico (como Pixel Piracy) ou mais aventura séria?

---

## 12. Glossário rápido (para quem está começando)
- **GDD**: este documento — a "bíblia" do jogo.
- **Core loop**: o ciclo de ações que o jogador repete e que deixa o jogo viciante.
- **MVP / Vertical slice**: a menor versão jogável que já mostra o jogo "de verdade".
- **Placeholder**: arte provisória só para testar.
- **Tilemap**: grade de quadradinhos usada para montar cenários.
- **ScriptableObject**: arquivo de dados do Unity (ex.: "Espada de ferro: dano 5, preço 20").
