namespace Translumo.Processing
{
    public interface IProcessingTextLocalizer
    {
        string Get(string key, params object[] args);
    }
}
