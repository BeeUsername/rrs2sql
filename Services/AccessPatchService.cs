using System;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Выполняет SQL-патчи для Access базы данных.
    /// Патчи берутся из папки access_patches.
    /// </summary>
    public class AccessPatchService
    {
        /// <summary>
        /// Путь к текущему лог-файлу выполнения патчей.
        /// </summary>
        private string _logFile;

        /// <summary>
        /// Выполняет все SQL-патчи для указанной MDB базы.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к Access базе данных.
        /// </param>
        public void ExecutePatches(string mdbPath)
        {
            // Каталог с sql-патчами
            string patchesFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "access_patches");

            // Если папки нет — патчить нечего
            if (!Directory.Exists(
                patchesFolder))
            {
                return;
            }

            // Получаем список *.sql файлов
            // и сортируем их по имени
            string[] files =
                Directory.GetFiles(
                    patchesFolder,
                    "*.sql")
                .OrderBy(Path.GetFileName)
                .ToArray();

            // Если файлов нет — завершаем работу
            if (files.Length == 0)
            {
                return;
            }

            // Папка логов
            string logsFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs");

            Directory.CreateDirectory(
                logsFolder);

            // Создаем лог текущего запуска
            _logFile =
                Path.Combine(
                    logsFolder,
                    $"AccessPatchLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            // Заголовок лога
            Log("===== НАЧАЛО ПРИМЕНЕНИЯ ACCESS ПАТЧЕЙ =====");
            Log("");
            Log($"MDB: {mdbPath}");
            Log("");
            Log("===========================================");
            Log("");

            // Флаг наличия ошибок
            bool hasErrors = false;

            // Строка подключения к Access
            string connectionString =
                $"Provider=Microsoft.Jet.OLEDB.4.0;" +
                $"Data Source={mdbPath};";

            // Открываем подключение к MDB
            using (var connection =
                new OleDbConnection(
                    connectionString))
            {
                connection.Open();

                // Выполняем каждый sql-файл
                foreach (string file in files)
                {
                    Log($"ФАЙЛ: {Path.GetFileName(file)}");
                    Log("");

                    try
                    {
                        // Читаем содержимое файла
                        string script =
                            File.ReadAllText(
                                file,
                                Encoding.UTF8);

                        // Пустые файлы пропускаем
                        if (string.IsNullOrWhiteSpace(
                            script))
                        {
                            Log("Файл пустой");
                            Log("");
                            continue;
                        }

                        // Разбиваем скрипт по GO
                        string[] batches =
                            SplitSqlBatches(
                                script);

                        // Выполняем каждый SQL-блок отдельно
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

                            // Пишем SQL в лог
                            Log("SQL:");
                            Log("");
                            Log(sql);
                            Log("");

                            try
                            {
                                using (var command =
                                    new OleDbCommand(
                                        sql,
                                        connection))
                                {
                                    // Без ограничения времени выполнения
                                    command.CommandTimeout =
                                        0;

                                    command.ExecuteNonQuery();
                                }

                                Log("РЕЗУЛЬТАТ: УСПЕШНО");
                                Log("");
                            }
                            catch (Exception ex)
                            {
                                hasErrors = true;

                                Log("РЕЗУЛЬТАТ: ОШИБКА");
                                Log("");
                                Log(ex.ToString());
                                Log("");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ошибка чтения файла
                        // или подготовки скрипта
                        hasErrors = true;

                        Log("КРИТИЧЕСКАЯ ОШИБКА ФАЙЛА");
                        Log("");
                        Log(ex.ToString());
                        Log("");
                    }

                    Log("===========================================");
                    Log("");
                }
            }

            Log("===== ЗАВЕРШЕНИЕ ACCESS ПАТЧЕЙ =====");

            // Если были ошибки —
            // сообщаем пользователю
            if (hasErrors)
            {
                throw new Exception(
                    "При выполнении Access-патчей возникли ошибки.\n\n" +
                    $"Подробности в логе:\n{_logFile}");
            }
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
        private void Log(string text)
        {
            File.AppendAllText(
                _logFile,
                //$"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
                $"{text}{Environment.NewLine}");
        }
    }
}


//using System;
//using System.Data.OleDb;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;

//namespace RRStoSQL.Services
//{
//	public class AccessPatchService
//	{
//		private string _logFile;

//		public void ExecutePatches(string mdbPath)
//		{
//			string patchesFolder =
//				Path.Combine(
//					AppDomain.CurrentDomain.BaseDirectory,
//					"access_patches");

//			if (!Directory.Exists(
//				patchesFolder))
//			{
//				return;
//			}

//			string[] files =
//				Directory.GetFiles(
//					patchesFolder,
//					"*.sql")
//				.OrderBy(Path.GetFileName)
//				.ToArray();

//			if (files.Length == 0)
//			{
//				return;
//			}

//			string logsFolder =
//				Path.Combine(
//					AppDomain.CurrentDomain.BaseDirectory,
//					"logs");

//			Directory.CreateDirectory(
//				logsFolder);

//			_logFile =
//				Path.Combine(
//					logsFolder,
//					$"AccessPatchLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

//			Log("===== НАЧАЛО ПРИМЕНЕНИЯ ACCESS ПАТЧЕЙ =====");
//			Log("");
//			Log($"MDB: {mdbPath}");
//			Log("");
//			Log("===========================================");
//			Log("");

//			bool hasErrors = false;

//			string connectionString =
//				$"Provider=Microsoft.Jet.OLEDB.4.0;" +
//				$"Data Source={mdbPath};";

//			using (var connection =
//				new OleDbConnection(
//					connectionString))
//			{
//				connection.Open();

//				foreach (string file in files)
//				{
//					Log($"ФАЙЛ: {Path.GetFileName(file)}");
//					Log("");

//					try
//					{
//						string script =
//							File.ReadAllText(
//								file,
//								Encoding.UTF8);

//						if (string.IsNullOrWhiteSpace(
//							script))
//						{
//							Log("Файл пустой");
//							Log("");
//							continue;
//						}

//						string[] batches =
//							SplitSqlBatches(
//								script);

//						foreach (string batch in batches)
//						{
//							string sql =
//								batch.Trim();

//							if (string.IsNullOrWhiteSpace(
//								sql))
//							{
//								continue;
//							}

//							Log("SQL:");
//							Log("");
//							Log(sql);
//							Log("");

//							try
//							{
//								using (var command =
//									new OleDbCommand(
//										sql,
//										connection))
//								{
//									command.CommandTimeout =
//										0;

//									command.ExecuteNonQuery();
//								}

//								Log("РЕЗУЛЬТАТ: УСПЕШНО");
//								Log("");
//							}
//							catch (Exception ex)
//							{
//								hasErrors = true;

//								Log("РЕЗУЛЬТАТ: ОШИБКА");
//								Log("");
//								Log(ex.ToString());
//								Log("");
//							}
//						}
//					}
//					catch (Exception ex)
//					{
//						hasErrors = true;

//						Log("КРИТИЧЕСКАЯ ОШИБКА ФАЙЛА");
//						Log("");
//						Log(ex.ToString());
//						Log("");
//					}

//					Log("===========================================");
//					Log("");
//				}
//			}

//			Log("===== ЗАВЕРШЕНИЕ ACCESS ПАТЧЕЙ =====");

//			if (hasErrors)
//			{
//				throw new Exception(
//					"При выполнении Access-патчей возникли ошибки.\n\n" +
//					$"Подробности в логе:\n{_logFile}");
//			}
//		}

//		private string[] SplitSqlBatches(
//			string script)
//		{
//			return Regex.Split(
//				script,
//				@"^\s*GO\s*$",
//				RegexOptions.Multiline |
//				RegexOptions.IgnoreCase);
//		}

//		private void Log(string text)
//		{
//			File.AppendAllText(
//				_logFile,
//                //$"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
//                $"{text}{Environment.NewLine}");
//        }
//	}
//}