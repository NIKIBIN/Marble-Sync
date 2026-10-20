# Tarefa atual

Nenhuma em andamento. O Checkpoint 1 (protótipo jogável) foi concluído; ver `ROADMAP.md`.

## Como regenerar o protótipo

- `Marble Sync/Build Prototype (levels + prefabs + scene)` recria as fases, materiais, prefabs e a cena `Assets/_Project/Scenes/MarbleSync.unity`, com todas as referências ligadas.
- `Marble Sync/1. Generate Levels` só regrava as fases (`Assets/_Project/Data/Levels`) e imprime no console se cada uma é resolvível.
- As fases são escritas em `Assets/_Project/Scripts/Editor/LevelGenerator.cs`.
