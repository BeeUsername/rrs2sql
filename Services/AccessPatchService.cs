using System;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RRStoSQL.Services
{
	public class AccessPatchService
	{
		private string _logFile;

		public void ExecutePatches(string mdbPath)
		{
			string patchesFolder =
				Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"access_patches");

			if (!Directory.Exists(
				patchesFolder))
			{
				return;
			}

			string[] files =
				Directory.GetFiles(
					patchesFolder,
					"*.sql")
				.OrderBy(Path.GetFileName)
				.ToArray();

			if (files.Length == 0)
			{
				return;
			}

			string logsFolder =
				Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"logs");

			Directory.CreateDirectory(
				logsFolder);

			_logFile =
				Path.Combine(
					logsFolder,
					$"AccessPatchLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

			Log("===== НАЧАЛО ПРИМЕНЕНИЯ ACCESS ПАТЧЕЙ =====");
			Log("");
			Log($"MDB: {mdbPath}");
			Log("");
			Log("===========================================");
			Log("");

			bool hasErrors = false;

			string connectionString =
				$"Provider=Microsoft.Jet.OLEDB.4.0;" +
				$"Data Source={mdbPath};";

			using (var connection =
				new OleDbConnection(
					connectionString))
			{
				connection.Open();

				foreach (string file in files)
				{
					Log($"ФАЙЛ: {Path.GetFileName(file)}");
					Log("");

					try
					{
						string script =
							File.ReadAllText(
								file,
								Encoding.UTF8);

						if (string.IsNullOrWhiteSpace(
							script))
						{
							Log("Файл пустой");
							Log("");
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

			if (hasErrors)
			{
				throw new Exception(
					"При выполнении Access-патчей возникли ошибки.\n\n" +
					$"Подробности в логе:\n{_logFile}");
			}
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

		private void Log(string text)
		{
			File.AppendAllText(
				_logFile,
                //$"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
                $"{text}{Environment.NewLine}");
        }
	}
}