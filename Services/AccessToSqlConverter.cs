using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;

namespace RRStoSQL.Services
{
	public class AccessToSqlConverter
	{

		/// <summary>
		/// Переносит данные из MDB в SQL
		/// </summary>
		public void CopyData(
			string mdbPath,
			string sqlInstance,
			string databaseName)
		{
			string accessConnection =
				$@"Provider=Microsoft.Jet.OLEDB.4.0;
Data Source={mdbPath};";

			string sqlConnection =
				$"Server={sqlInstance};" +
				$"Database={databaseName};" +
				$"Integrated Security=True;" +
				$"TrustServerCertificate=True;";

			using (var access =
				new OleDbConnection(
					accessConnection))

			using (var sql =
				new SqlConnection(
					sqlConnection))
			{
				access.Open();

				sql.Open();

				var tables =
					GetTables(
						mdbPath);

				foreach (string table
					in tables)
				{
					// читаем таблицу Access
					using (var accessCmd =
						new OleDbCommand(
							$"SELECT * FROM [{table}]",
							access))
					{
						using (var reader =
							accessCmd.ExecuteReader())
						{
							// SqlBulkCopy быстрее INSERT
							using (var bulk =
								new SqlBulkCopy(
									sql))
							{
								bulk.DestinationTableName =
									$"[{table}]";


								for (int i = 0;
									 i < reader.FieldCount;
									 i++)
								{
									bulk.ColumnMappings.Add(
										reader.GetName(i),
										reader.GetName(i));
								}

								try
								{
									bulk.WriteToServer(
										reader);
								}
								catch (Exception ex)
								{
									throw new Exception(
										$"Ошибка переноса данных\n\n" +
										$"Таблица: {table}\n" +
										$"Строк: {reader.RecordsAffected}\n\n" +
										ex.ToString());
								}
							}
						}
					}
				}
			}
		}



		/// <summary>
		/// Создать SQL базу
		/// </summary>
		public void CreateDatabase(
			string sqlInstance,
			string databaseName)
		{
			string connectionString =
				$"Server={sqlInstance};" +
				$"Integrated Security=True;" +
				$"TrustServerCertificate=True;";

			using (var connection =
				new SqlConnection(
					connectionString))
			{
				connection.Open();

				string sql =
$@"
IF DB_ID('{databaseName}') IS NULL
BEGIN
    CREATE DATABASE [{databaseName}]
END
";

				using (var command =
					new SqlCommand(
						sql,
						connection))
				{
					command.ExecuteNonQuery();
				}
			}
		}

		/// <summary>
		/// Получить список таблиц из MDB
		/// </summary>
		public List<string> GetTables(
			string mdbPath)
		{
			var tables =
				new List<string>();

			string connectionString =
				$@"Provider=Microsoft.Jet.OLEDB.4.0;
Data Source={mdbPath};";

			using (var connection =
				new OleDbConnection(
					connectionString))
			{
				connection.Open();

				DataTable schema =
					connection.GetSchema(
						"Tables");

				foreach (
					DataRow row
					in schema.Rows)
				{
					string tableName =
						row["TABLE_NAME"]
						.ToString();

					string tableType =
						row["TABLE_TYPE"]
						.ToString();

					// Только пользовательские таблицы
					if (
						tableType ==
						"TABLE"
						&&
						!tableName.StartsWith(
							"MSys"))
					{
						tables.Add(
							tableName);
					}
				}
			}

			return tables;
		}

		/// <summary>
		/// Получает колонки таблицы Access
		/// </summary>
		private DataTable GetColumns(
			OleDbConnection connection,
			string tableName)
		{
			return connection.GetSchema(
				"Columns",
				new[]
				{
			null,
			null,
			tableName,
			null
				});
		}

		/// <summary>
		/// Преобразование типов Access → SQL
		/// </summary>
		private string ConvertType(
			int dataType,
			int length)
		{
			switch (dataType)
			{
				// Text
				case 130:
				case 202:

					// Access иногда возвращает 0
					if (length <= 0)
					{
						return "NVARCHAR(MAX)";
					}

					// SQL максимум 4000
					if (length > 4000)
					{
						return "NVARCHAR(MAX)";
					}

					return $"NVARCHAR({length})";

				// Memo / LongText
				case 203:
					return "NVARCHAR(MAX)";

				case 3:
					return "INT";

				case 2:
					return "SMALLINT";

				case 5:
					return "FLOAT";

				case 6:
					return "DECIMAL(18,2)";

				case 7:
					return "DATETIME";

				case 11:
					return "BIT";

				case 72:
					return "UNIQUEIDENTIFIER";

				default:
					return "NVARCHAR(MAX)";
			}
		}

		/// <summary>
		/// Создаёт SQL таблицы
		/// </summary>
		public void CreateTables(
			string mdbPath,
			string sqlInstance,
			string databaseName)
		{
			string accessConnection =
				$@"Provider=Microsoft.Jet.OLEDB.4.0;
Data Source={mdbPath};";

			string sqlConnection =
				$"Server={sqlInstance};" +
				$"Database={databaseName};" +
				$"Integrated Security=True;" +
				$"TrustServerCertificate=True;";

			using (var access =
				new OleDbConnection(
					accessConnection))

			using (var sql =
				new SqlConnection(
					sqlConnection))
			{
				access.Open();

				sql.Open();

				var tables =
					GetTables(
						mdbPath);

				foreach (string table
					in tables)
				{
					DataTable columns =
						GetColumns(
							access,
							table);

					var sqlColumns =
						new List<string>();

					foreach (DataRow row
	in columns.Rows)
					{
						try
						{
							string name =
								row["COLUMN_NAME"]
								.ToString();

							object dataTypeValue =
								row["DATA_TYPE"];

							int dataType;

							if (!int.TryParse(
									dataTypeValue.ToString(),
									out dataType))
							{
								dataType = 202;
							}

							//string sqlType =
							//	ConvertType(
							//		dataType);

							int maxLength = 0;

							if (row["CHARACTER_MAXIMUM_LENGTH"]
								!= DBNull.Value)
							{
								maxLength =
									Convert.ToInt32(
										row["CHARACTER_MAXIMUM_LENGTH"]);
							}

							string sqlType =
								ConvertType(
									dataType,
									maxLength);
									//---
							sqlColumns.Add(
								$"[{name}] {sqlType}");
						}
						catch (Exception ex)
						{
							throw new Exception(
								$"Ошибка таблицы: {table}\n" +
								$"Поле: {row["COLUMN_NAME"]}\n\n" +
								ex.Message);
						}
					}

					string createSql =
		$@"CREATE TABLE [{table}]
(
{string.Join(",",
		sqlColumns)}
)";

					using (var command =
						new SqlCommand(
							createSql,
							sql))
					{
						command.ExecuteNonQuery();
					}
				}
			}
		}
	}
}