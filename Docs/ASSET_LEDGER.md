# Asset Ledger — placeholders

| Asset | Placeholder atual | Onde | Final desejado |
| --- | --- | --- | --- |
| Rampas / trilhos | Cubos esticados, cor da bolinha clareada | `Prefabs/TrackPiece.prefab` | Rampas modeladas com bordas |
| Bolinhas | Esfera URP Lit + casca transparente para revestimento | `Prefabs/Marble.prefab`, `GhostMarble.prefab` | Bolinhas de vidro com shaders de mel/óleo/água |
| Poças (mel, óleo, água) | Cilindro achatado colorido | `Prefabs/Element.prefab` → Puddle | Decal/poça com brilho |
| Lança-chamas | Cubo + cápsula laranja + luz pontual | `Element.prefab` → Flamethrower | Bico modelado + partículas de fogo |
| Porta / botão / sensor | Cubo, disco e aro coloridos, rótulo TextMesh | `Element.prefab` | Peças com ícone por cor |
| Curva quadrada | Placa escura no canto | `Element.prefab` → CurveCorner | Canto em L modelado |
| Gangorra | Prancha, portão e fulcro em primitivas | cena `Game/Seesaw` | Gangorra de madeira animada |
| Sons (clique, splash, fogo, derreter, clank, rangido, tambor, prato, falha) | Sintetizados em código no início | `SfxPlayer.cs` | Áudios gravados/mixados |
| Fonte do HUD | LegacyRuntime.ttf | `HUD` | Fonte própria (TMP) |
