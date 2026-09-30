using System.CommandLine;
using System.Text;

namespace VoiceRecogniseBot;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Bot texts and transcriptions are often non-ASCII; the Windows console defaults to a legacy code page.
        Console.OutputEncoding = Encoding.UTF8;

        // Only the long-running command logs to the console; the others print their own output.
        AppLog.Configure(logToConsole: args.FirstOrDefault() == "run");

        try
        {
            // Exceptions are reported below rather than by System.CommandLine, which prints stack traces.
            var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
            return await Cli.Build().Parse(args).InvokeAsync(invocation);
        }
        catch (Exception ex)
        {
            AppLog.Logger.Fatal(ex, "Unhandled error");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}
