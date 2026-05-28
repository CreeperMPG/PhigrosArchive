namespace PhigrosArchive.Abstractions
{
    public interface IDifficultyProvider
    {
        bool IsLoaded { get; }
        float? GetDifficulty(string songId, int difficultyIndex);
    }
}
