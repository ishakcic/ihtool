using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
namespace SistemYonetici
{
    public static class Sys
    {
        public static bool Advanced;
        static string NSudoPath => Path.Combine(AppContext.BaseDirectory, "NSudoLC.exe");
        public static bool HasNSudo => File.Exists(NSudoPath);

        public static (int c, string o) Run(string file, string args, bool priv = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file, Arguments = args, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            if (priv && Advanced && HasNSudo)
            {
                psi.Arguments = "-U:T -P:E -Wait \"" + file + "\" " + args;
                psi.FileName = NSudoPath;
            }
            using var p = Process.Start(psi);
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, o);
        }

        public static (int c, string o) Ps(string script, bool priv = false)
        {
            string full = "[Console]::OutputEncoding=[Text.Encoding]::UTF8;$ProgressPreference='SilentlyContinue';" + script;
            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            return Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + enc, priv);
        }

        public static string Err((int c, string o) r)
        {
            if (r.c == 0) return null;
            string t = (r.o ?? "").Trim();
            if (t.Length == 0) return "Hata kodu " + r.c;
            return t.Length > 200 ? t.Substring(0, 200) : t;
        }

        static (RegistryKey root, string sub) Split(string k)
        {
            int i = k.IndexOf('\\');
            return (k.Substring(0, i) == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser, k.Substring(i + 1));
        }
        public static int? GetD(string k, string n)
        {
            var (r, s) = Split(k);
            using var key = r.OpenSubKey(s);
            return key?.GetValue(n) is int v ? v : (int?)null;
        }
        public static void SetD(string k, string n, int v)
        {
            var (r, s) = Split(k);
            using var key = r.CreateSubKey(s);
            key.SetValue(n, v, RegistryValueKind.DWord);
        }
        public static void Del(string k, string n)
        {
            var (r, s) = Split(k);
            using var key = r.OpenSubKey(s, true);
            key?.DeleteValue(n, false);
        }

        public static string CreateRestorePoint()
        {
            Run("powershell.exe", "-NoProfile -Command \"Enable-ComputerRestore -Drive 'C:\\'\"");
            return Err(Ps("Checkpoint-Computer -Description 'SistemYonetici' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop"));
        }
    }
}
