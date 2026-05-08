namespace Translumo.Infrastructure.Language
{
    public class LanguageDescriptor
    {
        public Languages Language { get; set; }

        public string Code { get; set; }

        public string IsoCode { get; set; }

        public bool TranslationOnly { get; set; } = false;

        public bool Asian { get; set; } = false;

        public bool RegionalVariant { get; set; } = false;

        public override bool Equals(object obj)
        {
            var langDesc = obj as LanguageDescriptor;
            if (langDesc is null)
            {
                return false;
            }

            return langDesc.Language == this.Language;
        }

        public override int GetHashCode()
        {
            return Language.GetHashCode();
        }
    }
}
