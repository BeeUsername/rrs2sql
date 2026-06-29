using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace RRStoSQL.Services
{
    public class SsmsService
    {
        // ============================================
        // Проверяем установлен ли SSMS
        // ============================================
        public bool IsInstalled()
        {
            // 64-битные программы
            if (CheckRegistry(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                return true;
            }

            // 32-битные программы на x64 системе
            if (CheckRegistry(
                Registry.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                return true;
            }

            return false;
        }

        // ============================================
        // Поиск SQL Server Management Studio
        // в указанной ветке реестра
        // ============================================
        private bool CheckRegistry(
            RegistryKey root,
            string path)
        {
            using (var key =
                root.OpenSubKey(path))
            {
                if (key == null)
                {
                    return false;
                }

                foreach (string subKeyName in key.GetSubKeyNames())
                {
                    using (var subKey =
                        key.OpenSubKey(subKeyName))
                    {
                        string displayName =
                            subKey?.GetValue(
                                "DisplayName")
                            as string;

                        // Например:
                        // SQL Server Management Studio 20
                        // SQL Server Management Studio 21 Preview
                        if (!string.IsNullOrEmpty(displayName) &&
                            displayName.IndexOf(
                                "SQL Server Management Studio",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // ============================================
        // Устанавливаем SSMS если его еще нет
        // ============================================
        public void EnsureInstalled()
        {
            // Уже установлен любой версии
            if (IsInstalled())
            {
                return;
            }

            // Веб-инсталлер лежит рядом с нашей утилитой
            string installerPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "web_installers",
                    "vs_SSMS.exe");

            if (!File.Exists(installerPath))
            {
                throw new Exception(
                    "Не найден установщик SSMS:\n\n" +
                    installerPath);
            }

            var process =
                new Process();

            process.StartInfo.FileName =
                installerPath;


            // --passive
            // пользователь видит прогресс установки,
            // но не может ничего изменить
            //
            // --wait
            // инсталлер не завершится пока установка
            // полностью не закончится
            //
            // --norestart
            // запрещаем автоматическую перезагрузку
            process.StartInfo.Arguments =
                //"--install " +
                "--passive " +
                "--wait " +
                "--norestart";

            process.StartInfo.UseShellExecute =
                true;

            // Запрашиваем права администратора
            process.StartInfo.Verb =
                "runas";

            process.Start();

            // Ждем окончания установки
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    "Ошибка установки SSMS.\n\n" +
                    "ExitCode = " +
                    process.ExitCode);
            }
        }
    }
}