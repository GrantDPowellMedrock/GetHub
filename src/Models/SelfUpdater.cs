using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GetHub.Models
{
    /// <summary>
    /// In-place self update. Downloads the release asset matching the current
    /// OS/arch, extracts it, then hands off to a small script that waits for this
    /// process to exit, copies the new files over the install dir (preserving the
    /// portable <c>data/</c> folder), and relaunches the app.
    ///
    /// Windows is fully supported. On other platforms callers should fall back to
    /// opening the releases page in a browser.
    /// </summary>
    public static class SelfUpdater
    {
        public static bool IsSupported => OperatingSystem.IsWindows();

        public static string AssetNameFor(string version)
        {
            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            if (OperatingSystem.IsWindows())
                return $"gethub_{version}.win-{arch}.zip";
            if (OperatingSystem.IsMacOS())
                return $"gethub_{version}.osx-{arch}.zip";
            return $"gethub_{version}.linux-{arch}.zip";
        }

        public static async Task RunAsync(Version ver, Action<string> onStatus, CancellationToken token)
        {
            var version = ver.TagName.TrimStart('v', 'V');
            var assetName = AssetNameFor(version);

            ReleaseAsset asset = null;
            foreach (var a in ver.Assets)
            {
                if (a.Name != null && a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                {
                    asset = a;
                    break;
                }
            }

            if (asset == null || string.IsNullOrEmpty(asset.DownloadUrl))
                throw new Exception($"No downloadable build found for your platform ({assetName}).");

            var tmpRoot = Path.Combine(Path.GetTempPath(), "GetHubUpdate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpRoot);
            var zipPath = Path.Combine(tmpRoot, assetName);

            onStatus("Downloading update…");
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GetHub");
                using var resp = await client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(zipPath);
                await resp.Content.CopyToAsync(fs, token);
            }

            onStatus("Extracting…");
            var extractDir = Path.Combine(tmpRoot, "extract");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // CI zips wrap the payload in a top-level "GetHub" folder.
            var newFilesDir = Path.Combine(extractDir, "GetHub");
            if (!Directory.Exists(newFilesDir))
                newFilesDir = extractDir;

            onStatus("Installing… GetHub will restart.");
            var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var exePath = Process.GetCurrentProcess().MainModule!.FileName;
            var pid = Environment.ProcessId;

            if (OperatingSystem.IsWindows())
            {
                var script = Path.Combine(tmpRoot, "apply_update.ps1");
                File.WriteAllText(script, WindowsScript);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-ExecutionPolicy");
                psi.ArgumentList.Add("Bypass");
                psi.ArgumentList.Add("-WindowStyle");
                psi.ArgumentList.Add("Hidden");
                psi.ArgumentList.Add("-File");
                psi.ArgumentList.Add(script);
                psi.ArgumentList.Add("-AppPid");
                psi.ArgumentList.Add(pid.ToString());
                psi.ArgumentList.Add("-Src");
                psi.ArgumentList.Add(newFilesDir);
                psi.ArgumentList.Add("-Dst");
                psi.ArgumentList.Add(installDir);
                psi.ArgumentList.Add("-Exe");
                psi.ArgumentList.Add(exePath);
                Process.Start(psi);

                // Release the exe lock so the swapper can overwrite it.
                Environment.Exit(0);
            }
        }

        // Waits for the app to exit, copies new files over the install dir while
        // NEVER touching the "data" folder (preferences), then relaunches.
        private const string WindowsScript = @"
param(
  [int]$AppPid,
  [string]$Src,
  [string]$Dst,
  [string]$Exe
)
try { Wait-Process -Id $AppPid -Timeout 30 -ErrorAction SilentlyContinue } catch {}
Start-Sleep -Milliseconds 800
robocopy $Src $Dst /E /XD data /R:5 /W:1 | Out-Null
Start-Sleep -Milliseconds 300
Start-Process -FilePath $Exe
";
    }
}
