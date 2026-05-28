namespace PhigrosArchive.Abstractions
{
    public interface ISaveLogger
    {
        void Info(string message);
        void Success(string message);
        void Warning(string message);
        void Error(string message);
        void Debug(string message);
        void Progress(string message);
    }
}
