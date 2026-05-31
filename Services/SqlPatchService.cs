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
					try
					{
						string script =
							File.ReadAllText(
								file);

						if (string.IsNullOrWhiteSpace(
							script))
						{
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

							using (var command =
								new SqlCommand(
									sql,
									connection))
							{
								command.CommandTimeout =
									0;

								command.ExecuteNonQuery();
							}
						}
					}
					catch (Exception ex)
					{
						if (ex.ToString().Contains(
							"There is already an object named"))
						{
							return;
						}

						throw new Exception(
							$"Ошибка выполнения SQL-патча:{Environment.NewLine}{Environment.NewLine}" +
							$"{Path.GetFileName(file)}{Environment.NewLine}{Environment.NewLine}" +
							ex.Message,
							ex);
					}
				}
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
	}
}