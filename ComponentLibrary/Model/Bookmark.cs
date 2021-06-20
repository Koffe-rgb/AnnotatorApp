using Kumo;

namespace ComponentLibrary.Model
{
    // Каждый Bookmark модлеирует одно Property
    public class Bookmark
    {
        public int Start { get; }
        public int End { get; }
        public string Type { get; set; }
        public string Text { get; set; }
        public string Literal { get; set; }
        public Range Range { get; set; }

        public Bookmark(int start, int end, string type, string literal, Range range)
        {
            Start = start;
            End = end;
            Literal = literal;
            Type = type;
            Text = range?.Text();
            Range = range;
        }

        public Bookmark(int start, int end, string type, string literal, string text)
        {
            Start = start;
            End = end;
            Literal = literal;
            Type = type;
            Text = text;
            Range = null;
        }

        public Bookmark Clone()
        {
            return new(Start, End, Type, Literal, Range);
        }
    }
}