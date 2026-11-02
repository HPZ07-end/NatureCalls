using System;

namespace NatureCalls.Core
{
    /// <summary>
    /// Resolves the host's map choice. <see cref="Random"/> stands for the "随机地图" option.
    /// </summary>
    public static class MapSelection
    {
        public const int Random = -1;

        public static bool IsValid(int choice, int mapCount) =>
            mapCount > 0 && (choice == Random || (choice >= 0 && choice < mapCount));

        /// <param name="nextRandom">Returns a value in [0, mapCount).</param>
        public static int Resolve(int choice, int mapCount, Func<int, int> nextRandom)
        {
            if (!IsValid(choice, mapCount))
                throw new ArgumentOutOfRangeException(nameof(choice));
            if (choice != Random)
                return choice;
            var index = nextRandom(mapCount);
            if (index < 0 || index >= mapCount)
                throw new InvalidOperationException("Random map index is out of range.");
            return index;
        }
    }
}
