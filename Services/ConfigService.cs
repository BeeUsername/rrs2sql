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
            var map =
                new ExeConfigurationFileMap
                {
                    ExeConfigFilename =
                        configPath
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
                      {
                          try
                          {
                              var builder =
                                  new OleDbConnectionStringBuilder(
                                      x.ConnectionString);

                              string provider =
                                  builder["Provider"]?.ToString();

                              return
                                  provider.Equals(
                                      "Microsoft.Jet.OLEDB.4.0",
                                      StringComparison.OrdinalIgnoreCase)
                                  ||
                                  provider.StartsWith(
                                      "Microsoft.ACE.OLEDB",
                                      StringComparison.OrdinalIgnoreCase);
                          }
                          catch
                          {
                              return false;
                          }
                      });
            
            if (connection == null)
            {
                throw new Exception(
                    "Строка подключения MDB не найдена");
            }

            // Читаем секцию ProgramSettings
            XDocument doc =
                XDocument.Load(
                    configPath);

            var programSettings =
                doc.Descendants(
                    "ProgramSettings")
                .FirstOrDefault();

            return new AppConfig
            {
                ConfigPath =
                    configPath,

                ConnectionString =
                    connection.ConnectionString,

                // Получаем полный путь до MDB
                MdbPath =
                    ExtractMdbPath(
                        connection.ConnectionString,
                        configPath),

                // Тип БД Access/Mssql
                DbType =
                    programSettings?
                        .Attribute("DbType")
                        ?.Value,

                // Логин веб-сервиса используется
                // для формирования имени SQL базы
                WebServiceLogin =
                    programSettings?
                        .Attribute("WebServiceLogin")
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

            // =====================================================
            // Создаем резервную копию конфига
            // =====================================================

            CreateConfigBackup(
                configPath);

            // =====================================================
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
                BuildLocalSqlConnectionString(
                    appConfig);

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
		//public string BuildSqlConnectionString(
		//	AppConfig appConfig)
		//{
		//	return
		//		"Provider=SQLOLEDB;" +
		//		$"Data Source={Environment.MachineName}" +
		//		//"Data Source=localhost\\SQLEXPRESS,1433;" +
		//		$"\\SQLEXPRESS,1433;" +
		//		"Password=RRS_999999999!;" +
		//		"User ID=RRUser;" +
		//		$"Initial Catalog={appConfig.SqlDatabaseName}";
		//}

        
        //Копирование строки подключения к SQL Server в буфер обмена
        public void CopyConnectionString(
            AppConfig appConfig)
                {
                    string connectionString =
                        BuildRemoteSqlConnectionString(
                            appConfig);

                    string xml =
                $@"<connectionStrings>
            <add
                name=""Citilab.RemoteReg.Properties.Settings.CitilabConnectionString""
                connectionString=""{connectionString}""
                providerName=""System.Data.OleDb"" />
        </connectionStrings>";

                    Clipboard.SetText(
                        xml);
                }

        //Билдер для подключения к SQL Server на удаленном ПК (для копирования в буфер обмена)
        private string BuildRemoteSqlConnectionString(
            AppConfig appConfig)
        {
            return
                "Provider=SQLOLEDB;" +
                $"Data Source={Environment.MachineName}\\SQLEXPRESS,1433;" +
                "User ID=RRUser;" +
                "Password=RRS_999999999!;" +
                $"Initial Catalog={appConfig.SqlDatabaseName}";
        }

        //Билдер для подключения к SQL Server на локальном ПК (для переключения конфига на SQL)
        private string BuildLocalSqlConnectionString(
            AppConfig appConfig)
                {
                    return
                        "Provider=SQLOLEDB;" +
                        "Data Source=LOCALHOST\\SQLEXPRESS,1433;" +
                        "User ID=RRUser;" +
                        "Password=RRS_999999999!;" +
                        $"Initial Catalog={appConfig.SqlDatabaseName}";
                }

        
        //фукция создания резервной копии конфига
        private void CreateConfigBackup(
            string configPath)
                {
            string folder =
                Path.GetDirectoryName(
                    configPath);

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(
                    configPath);

            string extension =
                Path.GetExtension(
                    configPath);

            string backupPath =
                Path.Combine(
                    folder,
                    fileNameWithoutExtension +
                    "_Access_Backup" +
                    extension);

            int counter = 1;

            while (File.Exists(
                backupPath))
            {
                backupPath =
                    Path.Combine(
                        folder,
                        fileNameWithoutExtension +
                        "_Access_Backup_" +
                        counter +
                        extension);

                counter++;
            }

            File.Copy(
                configPath,
                backupPath);
        }


    }
}