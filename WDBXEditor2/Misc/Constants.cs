using System.Reflection;

namespace WDBXEditor2
{
    public static class Constants
    {
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }
}
