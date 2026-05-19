using RRStoSQL.Models;
using System.Configuration;
using System.IO;

namespace RRStoSQL.Services
{
	public class ConfigService
	{
		public AppConfig Read(string configPath)
		{
			var map = new ExeConfigurationFileMap();

			map.ExeConfigFilename = configPath;

			Configuration config =
				ConfigurationManager.OpenMappedExeConfiguration(
					map,
					ConfigurationUserLevel.None);

			var connection =
				config.ConnectionStrings
				.ConnectionStrings[0];

			return new AppConfig
			{
				ConfigPath = configPath,
				ConnectionString =
					connection.ConnectionString,

				MdbPath =
					ExtractMdbPath(
						connection.ConnectionString),

				DbType =
					config.AppSettings.Settings["DBType"]?.Value
			};
		}

		private string ExtractMdbPath(
			string connectionString)
		{
			var builder =
				new System.Data.OleDb.OleDbConnectionStringBuilder(
					connectionString);

			return builder.DataSource;
		}
	}
}