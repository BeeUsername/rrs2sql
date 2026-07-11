using System;
using System.Diagnostics;
using System.IO;
using RRStoSQL.Models;

namespace RRStoSQL.Services.Migration
{
    /// <summary>
    /// Запускает RemoteReg после завершения миграции.
    /// </summary>
    public class RemoteRegLauncher
    {
        public void StartRemoteReg(
            AppConfig appConfig)
        {
            string launcherPath =
                appConfig.ExePath.Replace(
                    "RemoteReg.exe",
                    "RemoteReg.Launcher.exe");

            if (!File.Exists(
                launcherPath))
            {
                throw new Exception(
                    "RemoteReg.Launcher.exe не найден.");
            }

            var process =
                new Process();

            process.StartInfo.FileName =
                launcherPath;

            process.StartInfo.WorkingDirectory =
                appConfig.AppFolder;

            process.Start();
        }
    }
}