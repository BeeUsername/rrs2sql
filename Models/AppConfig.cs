using System.IO;

namespace RRStoSQL.Models
{
	/// <summary>
	/// Параметры приложения УР
	/// и данные миграции.
	/// </summary>
	public class AppConfig
	{
		/// <summary>
		/// Полный путь к RemoteReg.exe.config
		/// </summary>
		public string ConfigPath { get; set; }

		/// <summary>
		/// Исходная строка подключения Access
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// Полный путь MDB
		/// </summary>
		public string MdbPath { get; set; }

		/// <summary>
		/// Access / Mssql
		/// </summary>
		public string DbType { get; set; }

		/// <summary>
		/// Каталог программы УР
		/// </summary>
		public string AppFolder
		{
			get
			{
				return Path.GetDirectoryName(
					ConfigPath);
			}
		}

		/// <summary>
		/// Путь к RemoteReg.exe
		/// </summary>
		public string ExePath
		{
			get
			{
				return Path.Combine(
					AppFolder,
					"RemoteReg.exe");
			}
		}

		/// <summary>
		/// Имя новой SQL базы
		/// </summary>
		public string SqlDatabaseName { get; set; }

		/// <summary>
		/// SQL instance
		/// </summary>
		public string SqlInstance { get; set; }

		/// <summary>
		/// Новая SQL строка подключения
		/// </summary>
		public string SqlConnectionString { get; set; }
	}
}