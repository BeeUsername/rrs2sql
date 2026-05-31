using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RRStoSQL.Services
{
	public class SqlPatchService
	{
		public void ExecutePatches(
			string sqlInstance,
			string databaseName)
		{
			string patchesFolder =
				Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"sql_patches");

			if (!Directory.Exists(
				patchesFolder))
			{
				return;
			}

			string[] files =
				Directory.GetFiles(
					patchesFolder,
					"*.sql")
				.OrderBy(
					Path.GetFileName)
				.ToArray();

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

			string connectionString =
				$"Server={sqlInstance};" +
				$"Database={databaseName};" +
				"Trusted_Connection=True;" +
				"TrustServerCertificate=True;";

			using (var connection =
				new SqlConnection(
					connectionString))
			{
				connection.Open();

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
						string script =
							File.ReadAllText(
								file);

						if (string.IsNullOrWhiteSpace(
							script))
						{
							WriteLog(
								logFile,
								"Файл пустой");

							continue;
						}

						string[] batches =
							SplitSqlBatches(
								script);

						foreach (string batch in batches)
						{
							string sql =
								batch.Trim();

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
								// просто продолжаем
								if (ex.ToString().Contains(
									"There is already an object named"))
								{
									WriteLog(
										logFile,
										"ПРОПУЩЕНО: объект уже существует");

									continue;
								}

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

		private string[] SplitSqlBatches(
			string script)
		{
			return Regex.Split(
				script,
				@"^\s*GO\s*$",
				RegexOptions.Multiline |
				RegexOptions.IgnoreCase);
		}

		private void WriteLog(
			string logFile,
			string text)
		{
			File.AppendAllText(
				logFile,
				$"[{DateTime.Now:HH:mm:ss}] {text}" +
				Environment.NewLine +
				Environment.NewLine);
		}
	}
}