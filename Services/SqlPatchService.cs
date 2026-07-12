using RRStoSQL.Helpers;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Выполняет SQL-патчи после конвертации
    /// базы Access в SQL Server.
    /// </summary>
    public class SqlPatchService
    {
        /// <summary>
        /// Выполняет все SQL-скрипты из каталога
        /// sql_patches по порядку.
        /// </summary>
        /// <param name="sqlInstance">
        /// Имя экземпляра SQL Server.
        /// </param>
        /// <param name="databaseName">
        /// Имя базы данных, в которой будут
        /// выполняться патчи.
        /// </param>
        public void ExecutePatches(
            //bool isNewDatabase,
            string sqlInstance,
            string databaseName)
        {
            //путь до каталога с патчами
            string patchesFolder;

            
            //patchesFolder =
            //    Path.Combine(
            //        AppDomain.CurrentDomain.BaseDirectory,
            //        "sql_patches");

            patchesFolder = ResourcePaths.SqlPatches;


            // Если каталога нет —
            // патчи выполнять не нужно
            if (!Directory.Exists(
                patchesFolder))
            {
                return;
            }

            // Получаем список SQL-файлов
            // и сортируем по имени файла
            string[] files =
                Directory.GetFiles(
                    patchesFolder,
                    "*.sql")
                .OrderBy(
                    Path.GetFileName)
                .ToArray();

            // Нет файлов для выполнения
            if (files.Length == 0)
            {
                return;
            }

            // ============================================
            // Создаём лог
            // ============================================

            string logFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Logs");

            Directory.CreateDirectory(
                logFolder);

            // Имя лог-файла содержит дату и время
            string logFile =
                Path.Combine(
                    logFolder,
                    $"SqlPatch_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            WriteLog(
                logFile,
                "===== НАЧАЛО ПРИМЕНЕНИЯ ПАТЧЕЙ =====");

            WriteLog(
                logFile,
                $"Сервер: {sqlInstance}");

            WriteLog(
                logFile,
                $"База: {databaseName}");

            // Формируем строку подключения
            // к SQL Server
            string connectionString =
                $"Server={sqlInstance};" +
                $"Database={databaseName};" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True;";

            using (var connection =
                new SqlConnection(
                    connectionString))
            {
                // Открываем соединение
                connection.Open();

                // Выполняем патчи по порядку
                foreach (string file in files)
                {
                    WriteLog(
                        logFile,
                        $"==============================");

                    WriteLog(
                        logFile,
                        $"ФАЙЛ: {Path.GetFileName(file)}");

                    try
                    {
                        // Читаем содержимое SQL-файла
                        string script =
                            File.ReadAllText(
                                file);

                        // Пустые файлы пропускаем
                        if (string.IsNullOrWhiteSpace(
                            script))
                        {
                            WriteLog(
                                logFile,
                                "Файл пустой");

                            continue;
                        }

                        // Разбиваем скрипт по GO
                        string[] batches =
                            SplitSqlBatches(
                                script);

                        // Выполняем каждый блок отдельно
                        foreach (string batch in batches)
                        {
                            string sql =
                                batch.Trim();

                            // Пустые блоки пропускаем
                            if (string.IsNullOrWhiteSpace(
                                sql))
                            {
                                continue;
                            }

                            WriteLog(
                                logFile,
                                "SQL:");

                            WriteLog(
                                logFile,
                                sql);

                            try
                            {
                                using (var command =
                                    new SqlCommand(
                                        sql,
                                        connection))
                                {
                                    // Без ограничения времени выполнения
                                    command.CommandTimeout =
                                        0;

                                    command.ExecuteNonQuery();
                                }

                                WriteLog(
                                    logFile,
                                    "РЕЗУЛЬТАТ: УСПЕШНО");
                            }
                            catch (Exception ex)
                            {
                                WriteLog(
                                    logFile,
                                    "РЕЗУЛЬТАТ: ОШИБКА");

                                WriteLog(
                                    logFile,
                                    ex.ToString());

                                // Если объект уже существует,
                                // считаем это допустимой ситуацией
                                // и продолжаем выполнение
                                if (ex.ToString().Contains(
                                    "There is already an object named"))
                                {
                                    WriteLog(
                                        logFile,
                                        "ПРОПУЩЕНО: объект уже существует");

                                    continue;
                                }

                                // Остальные ошибки записываем в лог,
                                // но выполнение остальных патчей продолжаем
                                continue;

                                //throw;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog(
                            logFile,
                            "КРИТИЧЕСКАЯ ОШИБКА ФАЙЛА");

                        WriteLog(
                            logFile,
                            ex.ToString());

                        throw new Exception(
                            $"Ошибка выполнения SQL-патча:{Environment.NewLine}{Environment.NewLine}" +
                            $"{Path.GetFileName(file)}{Environment.NewLine}{Environment.NewLine}" +
                            $"Подробности см. в логе:{Environment.NewLine}" +
                            logFile,
                            ex);
                    }
                }
            }

            WriteLog(
                logFile,
                "===== ПАТЧИ ЗАВЕРШЕНЫ =====");
        }

        /// <summary>
        /// Разбивает SQL-скрипт на отдельные блоки
        /// по инструкции GO.
        /// </summary>
        private string[] SplitSqlBatches(
            string script)
        {
            return Regex.Split(
                script,
                @"^\s*GO\s*$",
                RegexOptions.Multiline |
                RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Добавляет строку в лог-файл.
        /// </summary>
        private void WriteLog(
            string logFile,
            string text)
        {
            File.AppendAllText(
                logFile,
                //$"[{DateTime.Now:HH:mm:ss}] {text}" +
                $"{text}" +
                Environment.NewLine +
                Environment.NewLine);
        }
    }
}


