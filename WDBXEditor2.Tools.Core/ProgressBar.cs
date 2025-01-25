using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;

namespace WDBXEditor2.Tools.Core
{
    /// <summary>
    /// An ASCII progress bar
    /// </summary>
    public class ProgressBar : IDisposable, IProgress<int>
    {
        public int BlockCount { get; set; } = 10;

        private readonly IConsole _console;
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string animation = @"|/-\";

        private readonly Timer timer;


        private int currentProgress = 0;
        private string currentText = string.Empty;
        private bool disposed = false;
        private int animationIndex = 0;

        public ProgressBar(IConsole console)
        {
            _console = console;
            timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report(int value)
        {
            // Make sure value is in [0..100] range
            value = Math.Max(0, Math.Min(100, value));
            Interlocked.Exchange(ref currentProgress, value);
        }

        public void Reset()
        {
            currentProgress = 0;
            _console.Write(new string('\b', currentText.Length));
            currentText = string.Empty;
        }

        private void TimerHandler(object? state)
        {
            lock (timer)
            {
                if (disposed) return;

                int progressBlockCount = (int)(currentProgress / 100.0f * BlockCount);
                string text = string.Format("[{0}{1}] {2,3}% {3}",
                    new string('#', progressBlockCount), new string('-', BlockCount - progressBlockCount),
                    currentProgress,
                    animation[animationIndex++ % animation.Length]);
                UpdateText(text);

                ResetTimer();
            }
        }

        private void UpdateText(string text)
        {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            StringBuilder outputBuilder = new();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.AsSpan(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            _console.Write(outputBuilder.ToString());
            currentText = text;
        }

        private void ResetTimer()
        {
            timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (timer)
            {
                disposed = true;
                _console.Write(new string('\b', currentText.Length));
            }
        }
    }
}