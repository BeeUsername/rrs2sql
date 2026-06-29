
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using static System.Resources.ResXFileRef;

namespace RRStoSQL.Services
{
    public class BullzipService
    {
        // Стандартный каталог установки Bullzip
        private const string InstallFolder =
            @"C:\Program Files (x86)\Bullzip\MS Access to MSSQL";

        // Основной исполняемый файл конвертера
        private const string ExeName =
            "msa2sql.exe";

        // Проверяет установлен ли Bullzip.
        // Если нет — устанавливает его.
        // Возвращает полный путь до msa2sql.exe
        public string EnsureInstalled()
        {
            string exePath =
                Path.Combine(
                    InstallFolder,
                    ExeName);

            // Bullzip уже установлен
            if (File.Exists(exePath))
            {
                // На всякий случай повторно устанавливаем
                // режим совместимости Windows 7
                SetWin7Compatibility(exePath);

                return exePath;
            }

            // Установщик лежит рядом с RRStoSQL.exe
            string installerPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Bullzip",
                    "msa2sql_5_5_0_281.exe");

            // Проверяем наличие установщика
            if (!File.Exists(installerPath))
            {
                throw new Exception(
                    "Не найден установщик Bullzip:\n\n" +
                    installerPath);
            }

            Process process =
                new Process();

            process.StartInfo.FileName =
                installerPath;

            // Тихая установка:
            // /VERYSILENT - без интерфейса
            // /SUPPRESSMSGBOXES - без диалоговых окон
            // /NORESTART - без перезагрузки
            process.StartInfo.Arguments =
                "/VERYSILENT " +
                "/SUPPRESSMSGBOXES " +
                "/NORESTART";

            // Используем ShellExecute для поддержки UAC
            process.StartInfo.UseShellExecute =
                true;

            // Запуск от имени администратора
            process.StartInfo.Verb =
                "runas";

            process.Start();

            // Ожидаем завершения установки
            process.WaitForExit();

            // Проверяем код завершения
            if (process.ExitCode != 0)
            {
                throw new Exception(
                    "Ошибка установки Bullzip.\n\n" +
                    "ExitCode=" +
                    process.ExitCode);
            }

            // Проверяем что exe действительно появился
            if (!File.Exists(exePath))
            {
                throw new Exception(
                    "Bullzip установлен, но msa2sql.exe не найден.");
            }

            // Включаем режим совместимости Windows 7
            SetWin7Compatibility(exePath);

            return exePath;
        }

        // Копирует лог Bullzip в папку logs нашей утилиты
        public void CopyBullzipLog()
        {
            // Стандартное расположение лога Bullzip
            string sourceLog =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    @"Bullzip\MS Access to MSSQL\msa2sql.log");

            // Если лога нет — просто выходим
            if (!File.Exists(sourceLog))
            {
                return;
            }

            // Папка логов нашей программы
            string logsFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs");

            // Создаем папку если её нет
            Directory.CreateDirectory(
                logsFolder);

            // Имя файла содержит дату и время
            string destLog =
                Path.Combine(
                    logsFolder,
                    $"msa2sql_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // Копируем лог с перезаписью
            File.Copy(
                sourceLog,
                destLog,
                true);
        }

        // Включает для msa2sql.exe:
        // - режим совместимости Windows 7
        // - запуск от имени администратора
        private void SetWin7Compatibility(
            string exePath)
        {
            const string keyPath =
                @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

            using (RegistryKey key =
                Registry.CurrentUser.CreateSubKey(
                    keyPath))
            {
                key.SetValue(
                    exePath,
                    "~ WIN7RTM RUNASADMIN",
                    RegistryValueKind.String);
            }
        }

        // Удаляет Bullzip если он установлен
        public void Uninstall()
        {
            string uninstallExe =
                Path.Combine(
                    InstallFolder,
                    "unins000.exe");

            // Bullzip уже удален
            if (!File.Exists(uninstallExe))
            {
                return;
            }

            var process =
                new Process();

            process.StartInfo.FileName =
                uninstallExe;

            // Тихое удаление
            process.StartInfo.Arguments =
                "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";

            process.StartInfo.UseShellExecute =
                true;

            // Требуются права администратора
            process.StartInfo.Verb =
                "runas";

            process.Start();

            // Ожидаем завершения удаления
            process.WaitForExit();

            // Проверяем успешность удаления
            if (process.ExitCode != 0)
            {
                throw new Exception(
                    "Ошибка удаления Bullzip.\n\n" +
                    "ExitCode=" +
                    process.ExitCode);
            }
        }
    }
}

