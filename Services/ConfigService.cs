using RRStoSQL.Models;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Data.OleDb;

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
	}
}