using System;
using System.Diagnostics;
using System.IO;

namespace FEALiTE2D.Tda;

/// <summary>Runs TDA on the text of a .@za input and returns the content of the .tda.json result.</summary>
public interface ITdaSolver
{
    string Run(string zaText);
}

/// <summary>TDA failed: <see cref="Code"/> = legacy error code (.XZG / exit code of the CLI).</summary>
public sealed class TdaException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

/// <summary>Runs the <c>tda</c> command line program (<c>tda -q --json input.@za</c>) in a temporary directory.</summary>
public sealed class TdaCliSolver : ITdaSolver
{
    public TdaCliSolver(string executablePath, string? workDirectory = null, bool keepFiles = false)
    {
        ExecutablePath = executablePath;
        WorkDirectory = workDirectory;
        KeepFiles = keepFiles;
    }

    public string ExecutablePath { get; }

    /// <summary>Directory for the run folders; null = system temp.</summary>
    public string? WorkDirectory { get; }

    /// <summary>Keep the .@za and result files (debugging).</summary>
    public bool KeepFiles { get; }

    public string Run(string zaText)
    {
        string dir = Path.Combine(WorkDirectory ?? Path.GetTempPath(), "tda-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.GetFullPath(Path.Combine(dir, "model.@za"));
            File.WriteAllText(input, zaText);
            var psi = new ProcessStartInfo(ExecutablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = dir
            };
            psi.ArgumentList.Add("-q");
            psi.ArgumentList.Add("--json");
            psi.ArgumentList.Add(input);
            using var p = Process.Start(psi) ?? throw new TdaException(-1, $"Cannot start {ExecutablePath}.");
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new TdaException(p.ExitCode, (stderr + stdout).Trim());
            return File.ReadAllText(Path.Combine(dir, "model.tda.json"));
        }
        finally
        {
            if (!KeepFiles)
            {
                try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
