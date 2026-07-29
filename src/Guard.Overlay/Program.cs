using System.Globalization;

namespace AdultContentShutdownGuard.Guard.Overlay;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CountdownOverlayApplicationContext(ReadDuration(args)));
    }

    private static TimeSpan ReadDuration(IEnumerable<string> args)
    {
        const int defaultDurationSeconds = 300;
        const int minimumDurationSeconds = 5;
        const int maximumDurationSeconds = 3600;

        var arguments = args.ToArray();
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (!string.Equals(arguments[index], "--duration-seconds", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(arguments[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                && seconds >= minimumDurationSeconds
                && seconds <= maximumDurationSeconds)
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        return TimeSpan.FromSeconds(defaultDurationSeconds);
    }
}
