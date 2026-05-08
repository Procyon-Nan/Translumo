namespace Translumo.Infrastructure
{
    public enum TextTypes : byte
    {
        Translation = 1,

        Info = 2,

        Error = 3
    }

    public enum ChatTextChangeKind : byte
    {
        Add = 1,

        Append = 2,

        Replace = 3
    }
}
