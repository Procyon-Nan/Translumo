using Translumo.Processing;
using Translumo.Utils;

namespace Translumo.Services
{
    public sealed class ProcessingTextLocalizer : IProcessingTextLocalizer
    {
        public string Get(string key, params object[] args)
        {
            var value = LocalizationManager.GetValue(key) ?? key;
            return args == null || args.Length == 0
                ? value
                : string.Format(value, args);
        }
    }
}
