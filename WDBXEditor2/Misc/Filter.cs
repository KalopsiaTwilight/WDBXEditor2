using System.Text.RegularExpressions;

namespace WDBXEditor2.Misc
{
    public enum FilterType
    {
        None,
        Contains,
        Exact,
        RegEx
    }

    public class Filter
    {
        public FilterType Type { get; set; } = FilterType.None;

        public string Column { get; set; } = string.Empty;
        public string Value { get;set; } = string.Empty;

        public Regex AsRegex { 
            get
            {
                _regEx ??= new Regex(Value);
                return _regEx;
            } 
        }

        private Regex _regEx;


    }
}
