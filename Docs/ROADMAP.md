# Roadmap — Marble Sync

Fonte de design: [`GDD - Marble Sync.md`](../GDD%20-%20Marble%20Sync.md).

## Checkpoint 1 — Protótipo jogável (feito, 2026-10-20)

- Simulação determinística em passo fixo de 60 Hz, sem física (`Assets/_Project/Scripts/Simulation`, assembly sem referência à engine).
- Elementos confirmados: rampa (aceleração pela inclinação), curva quadrada (×0,3), mel, óleo, água, lança-chamas (liga/desliga por botão), botão + porta lógica, sensor.
- Tabela de estados completa, incluindo as propostas marcadas no GDD (óleo↔mel se substituem, molhada perde a água para mel/óleo, em chamas + mel carameliza, em chamas + óleo não muda).
- Gangorra: 0,5 s até a metade em velocidade fixa, fecha aos 50%, quem está na rampa chega junto 0,25 s depois; bolinha em chamas na gangorra só derrete no final, sem derrota.
- Soltura manual (clique ou teclas 1–5) arredondada ao passo; caramelizada volta a rolar com novo clique.
- Timers QUANDO → AGUARDAR → SOLTAR: início, "quando X for solta", sensor e botão (clicar no sensor/botão com a bolinha selecionada substitui o arrastar).
- Esponja (antes da primeira soltura; reiniciar devolve poça e esponja).
- Reiniciar segurando 0,5 s (botão ou R), só aparece depois da primeira soltura.
- Juice: gangorra treme e range enquanto aberta; previsão invisível depois da última soltura liga tambor, zoom que enquadra todas as bolinhas e câmera lenta leve; prato no sucesso; "Vermelha chegou 0,18 s atrasada" no quase.
- Histórico de replay por entradas: lista "#7 — 0,41 s", assistir com controle de velocidade, fantasma, carregar como base.
- Dica de timer após 5 derrotas (sem contador visível), gerada a partir de uma solução conhecida.
- 12 fases greybox (1-1, 1-2, 1-4, 2-1, 2-2, 2-3, 2-5, 3-1, 3-3, 4-1, 4-2, 4-3), todas verificadas por um solver.

## Próximos checkpoints

1. Playtest das 12 fases e ajuste de `SimTuning` (velocidades, fatores de mel/óleo/fogo).
2. Fases que faltam para fechar os 4 mundos (1-3, 1-5, 2-4, 3-2, 3-4, 3-5, 4-4, 4-5).
3. Comparar duas tentativas lado a lado numa linha do tempo de chegadas (GDD, Histórico de replay).
4. Persistência do histórico entre sessões e marcação de replays desatualizados pela versão da fase.
5. Arte e áudio finais no lugar dos placeholders (ver `ASSET_LEDGER.md`).
