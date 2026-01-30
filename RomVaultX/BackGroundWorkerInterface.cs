namespace RomVaultX
{
    public class BgwText(string Text)
    {
        public string Text { get; private set; } = Text;
    }

    public class BgwText2(string Text)
    {
        public string Text { get; private set; } = Text;
    }

    public class BgwText3(string Text)
    {
        public string Text { get; private set; } = Text;
    }


    public class BgwSetRange(int MaxVal)
    {
        public int MaxVal { get; private set; } = MaxVal;
    }

    public class BgwSetRange2(int MaxVal)
    {
        public int MaxVal { get; private set; } = MaxVal;
    }


    public class BgwValue2(int Value)
    {
        public int Value { get; private set; } = Value;
    }


    public class BgwRange2Visible(bool Visible)
    {
        public bool Visible { get; private set; } = Visible;
    }

    public class BgwShowError(string filename, string error)
    {
        public string filename { get; private set; } = filename;
        public string error { get; private set; } = error;
    }


    public class BgwShowFix(string fixDir, string fixZip, string fixFile, ulong? size, string dir, string sourceDir, string sourceZip, string sourceFile)
    {
        public string FixDir { get; private set; } = fixDir;
        public string FixZip { get; private set; } = fixZip;
        public string FixFile { get; private set; } = fixFile;
        public string? Size { get; private set; } = size.ToString();
        public string Dir { get; private set; } = dir;
        public string SourceDir { get; private set; } = sourceDir;
        public string SourceZip { get; private set; } = sourceZip;
        public string SourceFile { get; private set; } = sourceFile;
    }

    public class BgwShowFixError(string FixError)
    {
        public string FixError { get; private set; } = FixError;
    }
}
