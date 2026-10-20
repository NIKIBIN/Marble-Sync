using System;
using UnityEngine;

namespace MarbleSync.Gameplay
{
    [CreateAssetMenu(menuName = "Marble Sync/Level Library", fileName = "LevelLibrary")]
    public sealed class LevelLibrary : ScriptableObject
    {
        [SerializeField] private LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        public int Count => levels.Length;

        public LevelDefinition Get(int index)
        {
            if (index < 0 || index >= levels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Library has {levels.Length} levels.");
            }
            return levels[index] != null
                ? levels[index]
                : throw new InvalidOperationException($"Level slot {index} is empty in {name}.");
        }

        public void Author(LevelDefinition[] orderedLevels)
        {
            levels = orderedLevels;
        }
    }
}
