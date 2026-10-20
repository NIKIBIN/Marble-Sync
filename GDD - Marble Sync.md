# GDD — Marble Sync

Oct 20, 2026 · @Gabriel gouveia mizukami nishida

## Visão geral

O jogador solta bolinhas em rampas e precisa que todas cheguem à gangorra final dentro de uma janela curta de tempo. Quem chega cedo demais levanta a gangorra e bloqueia as outras.

**Pilar de design:** o jogador nunca recebe a fórmula do tempo. Ele observa, cria uma hipótese ("o mel no início custa mais que no fim"), testa e ajusta. Toda decisão de design deve servir a esse ciclo.

**Regras de ouro**

- O mesmo trajeto, com as mesmas entradas, leva sempre o mesmo tempo (simulação determinística).
- Cada elemento afeta o tempo de um jeito diferente, e os efeitos não são simplesmente somáveis.
- Fases que apresentam uma mecânica usam poucas bolinhas; os puzzles de fechamento de cada mundo usam 5.

**Motor:** Unity 3D.

**Nome provisório:** Marble Sync.

## Controles

No início, acertar o timing dos cliques faz parte da habilidade. Mais adiante, o jogador desbloqueia timers programáveis, mas a soltura manual continua sempre disponível.

**Soltura manual**

- Clicar numa bolinha a solta na hora.
- O instante do clique é arredondado para o passo da simulação (ex.: 1/60 s). Assim o replay reproduz a tentativa com exatidão.

**Timers programáveis (desbloqueio)**

Cada timer é uma regra no formato **QUANDO → AGUARDAR → SOLTAR**:

> Quando a **Azul** passar pelo **Sensor 2**, aguarde **0,5 s** e solte a **Vermelha**.

- **Gatilhos possíveis:** início da tentativa, bolinha passa por um sensor, bolinha aperta um botão, outra bolinha é solta.
- **Atraso:** o jogador clica na bolinha antes de começar e ajusta com setas para cima e para baixo, em passos de 0,25 s.
- **Ação:** soltar uma bolinha. Futuramente também poderia abrir ou fechar uma porta.
- Por padrão, o timer conta a partir do início da tentativa. O jogador pode arrastá-lo até um obstáculo ou botão para que conte a partir da passagem de uma bolinha por ali. A fase controla a dificuldade escolhendo quais pontos existem.
- Manual e timers convivem: o jogador pode programar 3 bolinhas e soltar 2 na mão.

**Por que funciona:** timers transformam reflexo em raciocínio. O jogador para de pensar "quando eu clico" e passa a pensar "quanto tempo a Verde demora a mais que a Azul depois do Sensor 2".

**Reiniciar**

- O jogo nunca interrompe uma tentativa no meio, nem quando uma bolinha derrete ou é bloqueada.
- O botão de reiniciar só aparece depois que a primeira bolinha é solta.
- Para reiniciar, o jogador **segura o botão por 0,5 s**. Isso evita reinícios acidentais.

## Modelo de simulação

A bolinha segue um caminho de segmentos em passo fixo, sem motor de física. Isso garante tempos 100% reprodutíveis e fáceis de ajustar.

**Variáveis da bolinha**

| Variável | O que é |
| --- | --- |
| `velocidade` | Velocidade atual ao longo do caminho |
| `velMax` | Teto de velocidade; depende do revestimento |
| `aceleracao` | Quanto ganha por segundo; depende da inclinação do segmento e do revestimento |
| `revestimento` | Nenhum, mel, óleo ou molhada |
| `condicao` | Normal, caramelizada (parada até novo clique) ou em chamas (derrete após 1 s sem água) |

**Passo da simulação (60 Hz)**

1. `velocidade` se aproxima de `velMax` usando `aceleracao`.
2. A bolinha avança `velocidade × dt` no caminho.
3. Ao cruzar um elemento (curva, mel, sensor, fogo), aplica o efeito dele.
4. Ao chegar à gangorra, registra o tempo de chegada.

**Previsão:** como tudo é determinístico, o jogo pode simular a tentativa inteira à frente, de forma invisível. Isso alimenta o juice da gangorra (ver abaixo).

## Estados da bolinha

A bolinha tem um revestimento por vez; o último aplicado substitui o anterior, exceto quando a tabela diz outra coisa. Itens marcados com (proposta) ainda precisam de decisão.

| Estado atual ↓ / Passa por → | Mel | Óleo | Água | Lança-chamas |
| --- | --- | --- | --- | --- |
| **Normal** | Fica com mel | Fica com óleo | Fica molhada | **Derrete** → derrota (a tentativa segue até o fim) |
| **Mel** (lenta) | Sem mudança | Óleo substitui o mel (proposta) | Remove o mel, fica molhada | **Caramelizada**: para no lugar até o jogador clicar nela de novo; ao voltar a rolar, perde todos os efeitos (fica normal) |
| **Óleo** (rápida) | Mel substitui o óleo (proposta) | Sem mudança | Remove o óleo, fica molhada | **Em chamas**: ganha ainda mais velocidade e derrete após **1 s** se não tocar água (na rampa da gangorra, o contador para: ver Gangorra) |
| **Molhada** | Fica com mel, perde a água (proposta) | Fica com óleo, perde a água (proposta) | Sem mudança: a água **não acumula** | **Intacta**, mas a água evapora e ela volta ao normal (protege **uma vez**) |
| **Em chamas** (muito rápida) | Carameliza e para (proposta) | Sem mudança; o contador continua (proposta) | **Apaga o fogo**, fica molhada e volta à velocidade máxima normal, sem o bônus do fogo | Sem mudança |

**Combos que geram puzzles**

- **Óleo + fogo + água:** a bolinha dispara, e o jogador tem 1 s para fazê-la tocar a água. A água vira extintor.
- **Escudo de uso único:** como a água não acumula, dois lança-chamas exigem a sequência água → fogo → água → fogo. Duas águas seguidas antes do fogo não ajudam.
- **Caramelização como segunda soltura:** mel + fogo para a bolinha no meio do caminho, e o jogador decide quando ela volta a rolar. Isso cria um segundo ponto de timing, que só pode ser acionado por clique, nunca por timer.
- **Troca de velocidade por segurança:** a água remove um óleo útil. Molhar antes do fogo protege, mas deixa a bolinha mais lenta.

## Mecânicas e obstáculos

Cada elemento entra no cálculo do tempo de um jeito diferente. É isso que obriga o jogador a testar em vez de somar.

**Confirmadas**

| Elemento | Efeito na simulação | Como afeta o tempo |
| --- | --- | --- |
| Rampa reta | Acelera até `velMax` | Recupera velocidade perdida |
| Curva quadrada | `velocidade × 0,3` | Depende do contexto: custa mais se a bolinha chega rápida e o trecho seguinte é curto |
| Mel | `velMax` cai para \~50% | Proporcional ao resto do trajeto: mel cedo custa mais que mel tarde |
| Óleo | `velMax` e `aceleracao` sobem | Oposto do mel, também proporcional ao que falta |
| Água | Remove mel e óleo; deixa molhada; apaga fogo | Desfaz efeitos; o ganho depende da posição |
| Lança-chamas | Derrete; mel carameliza (para até novo clique); óleo vira "em chamas" (derrete em 1 s sem água); molhada passa intacta uma vez; pode ser ligado e desligado por botão | Falha imediata ou atalho arriscado |
| Porta lógica / botão | Botão colorido: quando uma bolinha passa, inverte todas as portas da mesma cor (abertas fecham, fechadas abrem) | O tempo de uma bolinha passa a depender de outra |

**Ferramenta do jogador: esponja**

- Remove 1 poça de mel, óleo ou água do caminho.
- Só pode ser usada **antes de soltar a primeira bolinha**.
- A quantidade por fase é configurável no level design.
- O ícone da esponja só aparece em fases que têm esponjas, disponíveis ou já usadas.
- Ao reiniciar a fase, as poças removidas e as esponjas voltam.
- Por enquanto, é o único jeito de o jogador alterar o percurso. Nenhum elemento é posicionável.
- Estreia no 2-5.

**Para depois da primeira versão**

| Elemento | Efeito na simulação | Por que é interessante |
| --- | --- | --- |
| Secador / ventilador | Remove o estado molhada | Tira o escudo contra fogo; obriga a pensar na ordem |
| Esteira | Move a bolinha em velocidade fixa, ignorando revestimento | "Normaliza" bolinhas diferentes: um ponto de sincronização natural |
| Loop | Exige velocidade mínima; abaixo dela a bolinha volta e perde tempo | Punição não linear: um pouco mais lenta custa muito mais |
| Mola / trampolim | Salta um trecho em tempo fixo, se chegar com velocidade mínima | Pula curvas e mel; o tempo do salto não depende da velocidade |
| Desvio alternado | Manda cada bolinha que passa para um lado diferente | A ordem de soltura muda o trajeto, e não só o tempo |
| Elevador | Espera a bolinha, sobe em tempo fixo | Uma fila: se duas chegam juntas, a segunda espera um ciclo |
| Areia | Remove o óleo, mas gruda no mel (mel + areia = mais lenta ainda) | Mesmo elemento, efeitos opostos conforme o estado |
| Gelo | Bolinha molhada desliza sem atrito (mais rápida); seca não muda | Dá à água um uso de velocidade, não só de proteção |

A primeira versão fica só com as confirmadas: com a caramelização e o escudo de uso único, elas já geram combinações suficientes para os 3 mundos. Esta lista fica guardada para expansões; esteira e loop são as candidatas mais fortes.

## Gangorra e juice

A fase é vencida quando as 5 bolinhas chegam dentro da janela de tolerância (0,5 s entre a primeira e a última).

**Regra da gangorra**

- A gangorra é uma rampa única, compartilhada por todas as bolinhas, e mais longa que as outras.
- Ao entrar nela, a bolinha anda em velocidade fixa e seus estados não afetam mais a velocidade: toda bolinha leva exatamente **0,5 s até a metade**.
- Quando qualquer bolinha passa dos **50%** da gangorra, ela se fecha e bloqueia quem ainda não entrou. É isso que cria a janela de 0,5 s.
- A partir desse instante, as bolinhas que já estão na rampa são aceleradas e **todas chegam ao final juntas, 0,25 s depois**. Cada uma recebe a velocidade `distânciaRestante ÷ 0,25 s`.
- Enquanto a gangorra está aberta, ela treme e range.
- **Bolinha em chamas na gangorra:** o contador de 1 s para. Ela só derrete ao chegar ao final, e isso **não** causa derrota. Puzzle possível: incendiar uma bolinha de propósito para ganhar velocidade, desde que ela alcance a gangorra em menos de 1 s.

**Tensão: o "quase"**

- O jogo usa a previsão (simulação à frente) para saber, antes da chegada, qual será a diferença entre a primeira e a última bolinha.
- A previsão só é usada depois que a última bolinha foi solta. Antes disso, o resultado ainda depende do jogador.
- Se a diferença prevista for menor que \~2× a janela: **tambor rufando**, e a câmera dá **zoom mantendo todas as bolinhas no quadro** (enquadra a caixa que contém todas, com margem).
- Quanto menor a diferença, mais intenso o rufar e mais lento o tempo nos últimos instantes (câmera lenta leve, opcional).

**Desfecho**

- **Sucesso:** prato de bateria, gangorra equilibrada, próxima fase.
- **Quase:** a gangorra sobe, mas o jogo mostra por quanto errou ("Vermelha chegou 0,18 s atrasada"). O jogador sabe que chegou perto e qual bolinha ajustar.

## Histórico de replay

Cada tentativa é salva como uma lista de entradas, não como vídeo. Como a simulação é determinística, reexecutar as entradas reproduz a tentativa exatamente, e cada registro ocupa poucos bytes.

**O que cada registro guarda**

- Versão da fase (se a fase mudar, replays antigos ficam marcados como desatualizados).
- Soltura manual de cada bolinha, no passo exato da simulação.
- Timers programados naquela tentativa.
- Resultado: tempo de chegada de cada bolinha, diferença total e motivo da falha (gangorra, derreteu).

**O que o jogador pode fazer**

- Ver a lista das tentativas com a diferença de cada uma (ex.: "#7 — 0,41 s").
- Assistir a qualquer tentativa de novo, com controle de velocidade.
- Mostrar uma tentativa antiga como **fantasma** rolando junto com a atual.
- Comparar duas tentativas lado a lado numa linha do tempo de chegadas.
- Carregar uma tentativa antiga como ponto de partida e mudar só uma coisa (o melhor amigo do método de hipótese e teste).

**Dica de timer**

- Existe só a partir do 3-3, quando os timers chegam. Fica bloqueada até o jogador perder a fase **5 vezes, por qualquer motivo (gangorra, derretimento)**.
- O jogo **não mostra** o número de derrotas nem quantas faltam. A dica simplesmente aparece quando é liberada.
- Ao ser aberta, sugere uma regra de timer pronta (ex.: "Quando a Azul passar pelo Sensor 2, aguarde 0,5 s e solte a Verde").

## Progressão de fases

São 4 mundos com 5 fases cada. Cada mundo apresenta suas mecânicas com poucas bolinhas e fecha com puzzles de 5 bolinhas. O Mundo 1 usa só o timing dos cliques manuais; os timers chegam no 3-3.

| Fase | Bolinhas | O que acontece |
| --- | --- | --- |
| **Mundo 1 — Timing** |  |  |
| 1-1 | 2 | Tutorial inicial do jogo |
| 1-2 | 3 | Percurso simples com algumas curvas |
| 1-3 | 4 | Mesma ideia, mais uma bolinha |
| 1-4 | 5 | Mesma ideia, 5 bolinhas |
| 1-5 | 5 | Emaranhado complexo de curvas e retas |
| **Mundo 2 — Mel, óleo e água** |  |  |
| 2-1 | 2 | Apresenta o mel |
| 2-2 | 2 | Apresenta o óleo |
| 2-3 | 3 | Apresenta a água |
| 2-4 | 5 | Puzzle com curvas, poças de mel e poças de água |
| 2-5 | 5 | Puzzle complexo: o jogador usa a **esponja** para remover 1 poça |
| **Mundo 3 — Portas lógicas e timers** |  |  |
| 3-1 | 2 | Apresenta as portas lógicas: uma bolinha passa no botão azul e inverte as portas da linha azul |
| 3-2 | 3 | Puzzle de ordem das bolinhas, com portas que bloqueiam caminhos |
| 3-3 | 4 | Apresenta os timers: clicar na bolinha antes de começar e ajustar com setas (±0,25 s) |
| 3-4 | 4 | Timers arrastados para obstáculos específicos (soltar após a bolinha passar por um ponto) |
| 3-5 | 5 | Level complexo com curvas e botões, em que os timers facilitam a solução |
| **Mundo 4 — Fogo** |  |  |
| 4-1 | 2 | Apresenta o lança-chamas, com um botão que o liga e desliga |
| 4-2 | 2 | Interações óleo + fogo e água + fogo |
| 4-3 | 3 | Interação mel + fogo (caramelização) |
| 4-4 | 5 | Timers combinados com mel, fogo, óleo e água |
| 4-5 | 5 | Tudo junto: mel, óleo, água, fogo, timers e botões |

**Notas para 5 bolinhas**

- Janela fixa de 0,5 s em todos os mundos. A dificuldade cresce pelos percursos, não por apertar a janela.
- Cada mundo sobe a quantidade de bolinhas aos poucos: o jogador aprende a mecânica com 2 e chega a 5 nos puzzles de fechamento.
- O 2-5 é a primeira vez que o jogador **altera o percurso** (com a esponja). Vale apresentar isso com clareza, já que até ali ele só clicava.
- Os timers chegam no 3-3, quando bolinhas com portas lógicas já pesam na mão. Assim, o jogador sente a necessidade antes de receber a ferramenta.
- A caramelização (4-3) entra como ferramenta, não só como armadilha: algumas fases devem ser resolvidas usando a parada de propósito.

## Questões em aberto

Nenhuma no momento. Todas as dúvidas anteriores foram respondidas e incorporadas acima.
