using RRStoSQL.Models;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Data.OleDb;
using System.Xml.Linq;
using System.Windows.Forms;

namespace RRStoSQL.Services
{
	public class ConfigService
	{
		/// <summary>
		/// Читает RemoteReg.exe.config
		/// и извлекает параметры подключения к БД.
		/// </summary>
		/// <param name="configPath">
		/// Полный путь до RemoteReg.exe.config
		/// </param>
		/// <returns>
		/// Объект с параметрами приложения
		/// </returns>
		public AppConfig Read(string configPath)
		{
			// Говорим ConfigurationManager,
			// что нужно читать внешний .config файл,
			// а не конфиг нашей утилиты.
			var map = new ExeConfigurationFileMap
			{
				ExeConfigFilename = configPath
			};

			Configuration config =
				ConfigurationManager
				.OpenMappedExeConfiguration(
					map,
					ConfigurationUserLevel.None);

			// В конфиге может быть несколько строк
			// подключения (например aspnetdb).
			// Ищем именно Access MDB.
			var connection =
	config.ConnectionStrings
		  .ConnectionStrings
		  .Cast<ConnectionStringSettings>()
		  .FirstOrDefault(x =>
			  x.ConnectionString
			   .IndexOf(
				   "Provider=Microsoft.Jet.OLEDB",
				   StringComparison.OrdinalIgnoreCase) >= 0);

			if (connection == null)
			{
				throw new Exception(
					"Строка подключения MDB не найдена");
			}

			return new AppConfig
			{
				ConfigPath = configPath,

				ConnectionString =
					connection.ConnectionString,

				// Получаем полный путь до MDB
				MdbPath =
					ExtractMdbPath(
						connection.ConnectionString,
						configPath),

				// DBType понадобится позже
				// при переключении Access → SQL
				DbType =
					config.AppSettings
						  .Settings["DBType"]
						  ?.Value
			};
		}

		/// <summary>
		/// Извлекает путь MDB из connectionString.
		/// </summary>
		private string ExtractMdbPath(
			string connectionString,
			string configPath)
		{
			var builder =
				new OleDbConnectionStringBuilder(
					connectionString);

			string dataSource =
				builder.DataSource?.ToString();

			if (string.IsNullOrWhiteSpace(dataSource))
			{
				return null;
			}

			// В УР используется:
			//
			// |DataDirectory|\Data\Data.mdb
			//
			// Нужно заменить |DataDirectory|
			// на каталог программы.
			if (dataSource.StartsWith(
				"|DataDirectory|",
				StringComparison.OrdinalIgnoreCase))
			{
				string appFolder =
					Path.GetDirectoryName(
						configPath);

				dataSource =
					dataSource.Replace(
						"|DataDirectory|",
						appFolder);
			}

			return Path.GetFullPath(
				dataSource);
		}

		/// <summary>
		/// Переключает конфиг на SQL Server
		/// </summary>
		public void SwitchToSql(
			string configPath,
			AppConfig appConfig)
		{
			XDocument doc =
				XDocument.Load(
					configPath);

			// =====================================================
			// Меняем DbType="Mssql"
			// =====================================================

			var programSettings =
	doc.Descendants("ProgramSettings")
	.FirstOrDefault();

			if (programSettings != null)
			{
				programSettings.SetAttributeValue(
					"DbType",
					"Mssql");
			}

			// =====================================================
			// Формируем строку подключения SQL
			// =====================================================

			string sqlConnectionString =
				"Provider=SQLOLEDB;" +
				$"Data Source={Environment.MachineName}" +
				$"\\SQLEXPRESS,1433;" +
				"Password=RRS_999999999!;" +
				"User ID=RRUser;" +
				$"Initial Catalog={appConfig.SqlDatabaseName}";

			// =====================================================
			// Ищем connectionStrings
			// =====================================================

			var connectionElement =
				doc.Descendants(
					"add")
				.FirstOrDefault(
					x =>
					{
						var attr =
							x.Attribute(
								"connectionString");

						if (attr == null)
						{
							return false;
						}

						string value =
							attr.Value;

						return value.IndexOf(
							"Provider=",
							StringComparison.OrdinalIgnoreCase)
							>= 0;
					});

			if (connectionElement == null)
			{
				throw new Exception(
					"connectionString не найден");
			}

			// =====================================================
			// Меняем connectionString
			// =====================================================

			connectionElement.SetAttributeValue(
				"connectionString",
				sqlConnectionString);

			// =====================================================
			// Сохраняем config
			// =====================================================

			doc.Save(
				configPath);
		}

		/// <summary>
		/// Формирует SQL connection string
		/// </summary>
		public string BuildSqlConnectionString(
			AppConfig appConfig)
		{
			return
				"Provider=SQLOLEDB;" +
				$"Data Source={Environment.MachineName}" +
				$"\\SQLEXPRESS,1433;" +
				"Password=RRS_999999999!;" +
				"User ID=RRUser;" +
				$"Initial Catalog={appConfig.SqlDatabaseName}";
		}

		/// <summary>
		/// Копирует строку подключения
		/// в буфер обмена
		/// </summary>
		public void CopyConnectionString(
	AppConfig appConfig)
		{
			string connectionString =
				BuildSqlConnectionString(
					appConfig);

			string xml =
		$@"<connectionStrings>
		 <add name=""Citilab.RemoteReg.Properties.Settings.CitilabConnectionString"" connectionString=""{connectionString}"" providerName=""System.Data.OleDb"" />
		</connectionStrings>";

			Clipboard.SetText(
				xml);
		}


	}
}