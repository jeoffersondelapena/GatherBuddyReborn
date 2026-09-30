#nullable enable
using System;
using System.IO;
using System.Threading;

namespace GatherBuddy.ForkLogic;

// Two game windows share the plugin's files. A file is written beside itself and swapped in, so the other window
// never reads half of one, and a read that collides with a write is tried again.
public static class SafeFile
{
    public static void Write(string path, string text)
    {
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, path, true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception)
            {
            }

            throw;
        }
    }

    public static string Read(string path, int attempts = 5, int waitMs = 100)
    {
        for (var attempt = 1;; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (attempt < attempts && File.Exists(path))
            {
                Thread.Sleep(waitMs);
            }
        }
    }
}
