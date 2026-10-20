# Asset Ledger — placeholders

| Asset | Placeholder atual | Onde | Final desejado |
| --- | --- | --- | --- |
| Trilhos | Half-pipe procedural (malha gerada por código), cor da bolinha clareada | `Prefabs/HalfPipe.prefab`, `HalfPipeMesh.cs` | Half-pipe com material final |
| Bolinhas | Esfera URP Lit + casca transparente para revestimento | `Prefabs/Marble.prefab`, `GhostMarble.prefab` | Bolinhas de vidro com shaders de mel/óleo/água |
| Poças (mel, óleo, água) | Cilindro achatado colorido | `Prefabs/Element.prefab` → Puddle | Decal/poça com brilho |
| Lança-chamas | Cubo + cápsula laranja + luz pontual | `Element.prefab` → Flamethrower | Bico modelado + partículas de fogo |
| Porta / botão / sensor | Cubo, disco e aro coloridos, rótulo TextMesh | `Element.prefab` | Peças com ícone por cor |
| Gangorra | Prancha com um half-pipe por bolinha, portão e fulcro em primitivas | cena `Game/Seesaw` | Gangorra de madeira animada |
| Sons (clique, splash, fogo, derreter, clank, rangido, tambor, prato, falha) | Sintetizados em código no início | `SfxPlayer.cs` | Áudios gravados/mixados |
| Fonte do HUD | LegacyRuntime.ttf | `HUD` | Fonte própria (TMP) |
