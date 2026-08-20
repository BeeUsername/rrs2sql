using Microsoft.Win32;
using RRStoSQL.Helpers;
using RRStoSQL.Models;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

//using System.Threading;
using System.Windows.Forms;



namespace RRStoSQL.Services
{
	/// <summary>
	/// Работа с SQL Server
	/// </summary>
	public class SqlService
	{

		/// <summary>
		/// Создаёт пользователя в базе
		/// и выдаёт права
		/// </summary>
		public void GrantDatabaseRights(
			string sqlInstance,
			string databaseName)
		{
			string connectionString =
				$"Server={sqlInstance};" +
				$"Database={databaseName};" +
				$"Integrated Security=True;" +
				$"TrustServerCertificate=True;";

			using (var connection =
				new SqlConnection(
					connectionString))
			{
				connection.Open();

				string sql =
							@"
					IF NOT EXISTS
					(
						SELECT *
						FROM sys.database_principals
						WHERE name='RRUser'
					)
					BEGIN
						CREATE USER RRUser
						FOR LOGIN RRUser
					END

					ALTER ROLE db_owner
					ADD MEMBER RRUser
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
        /// Перезапускает SQL службу
        /// </summary>
        public void RestartSqlService(
			string sqlInstance)
        {
            // .\SQLEXPRESS -> MSSQL$SQLEXPRESS
            string serviceName =
                "MSSQL$" +
                sqlInstance.Replace(
                    @".\",
                    "");

            using (var service =
                new ServiceController(
                    serviceName))
            {
                service.Refresh();

                // Если служба уже работает —
                // останавливаем её
                if (service.Status !=
                    ServiceControllerStatus.Stopped)
                {
                    service.Stop();

                    service.WaitForStatus(
                        ServiceControllerStatus.Stopped,
                        TimeSpan.FromMinutes(2));
                }

                service.Refresh();

                // Запускаем только если действительно остановлена
                if (service.Status ==
                    ServiceControllerStatus.Stopped)
                {
                    service.Start();

                    service.WaitForStatus(
                        ServiceControllerStatus.Running,
                        TimeSpan.FromMinutes(2));
                }
            }
        }
        
        /// <summary>
        /// Создаёт логин RRUser и пользователя
        /// </summary>
        /// 
        public void CreateRRUser(string sqlInstance)
		{
			string connectionString =
				$"Server={sqlInstance};" +
				$"Integrated Security=True;" +
				$"TrustServerCertificate=True;";

			using (var connection = new SqlConnection(connectionString))
			{
				connection.Open();

									string sql = @"
					IF EXISTS(
						SELECT *
						FROM sys.server_principals
						WHERE name='RRUser'
					)
					BEGIN
						-- Если существует - меняем пароль
						ALTER LOGIN RRUser
						WITH PASSWORD='RRS_999999999!';
					END
					ELSE
					BEGIN
						-- Если не существует - создаем
						CREATE LOGIN RRUser
						WITH PASSWORD='RRS_999999999!';
					END
					";

				using (var command = new SqlCommand(sql, connection))
				{
					command.ExecuteNonQuery();
				}
			}
		}

		
		/// <summary>
		/// Ищет установленный SQLEXPRESS
		/// </summary>

		public SqlInstanceInfo FindSqlExpress()
		{
			// Проверяем 64-битный раздел
			var sql =
				FindSqlExpressInternal(
					RegistryView.Registry64);

			if (sql.Exists)
			{
				return sql;
			}

			// Проверяем 32-битный раздел
			return FindSqlExpressInternal(
				RegistryView.Registry32);
		}

		/// <summary>
		/// Поиск SQL в конкретном представлении реестра
		/// </summary>
		private SqlInstanceInfo FindSqlExpressInternal(
			RegistryView view)
		{
			using (var baseKey =
				RegistryKey.OpenBaseKey(
					RegistryHive.LocalMachine,
					view))
			{
				using (var key =
					baseKey.OpenSubKey(
						@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"))
				{
					if (key == null)
					{
						return new SqlInstanceInfo
						{
							Exists = false
						};
					}

					foreach (string name
						in key.GetValueNames())
					{
						if (name.Equals(
							"SQLEXPRESS",
							StringComparison.OrdinalIgnoreCase))
						{
							return new SqlInstanceInfo
							{
								Exists = true,
								InstanceName =
									@".\SQLEXPRESS"
							};
						}
					}
				}
			}

			return new SqlInstanceInfo
			{
				Exists = false
			};
		}

		/// <summary>
		/// Включает Mixed Authentication
		/// (Windows + SQL логины)
		/// </summary>
		/// <summary>
		/// Включает Windows + SQL Authentication
		/// </summary>
		public void EnableMixedAuthentication(
			string sqlInstance)
		{
			// .\SQLEXPRESS -> SQLEXPRESS
			string instanceName =
				sqlInstance.Replace(
					@".\",
					"");

			// Ищем внутренний ID:
			// MSSQL17.SQLEXPRESS
			string instanceId =
				GetInstanceId(
					instanceName);

			if (string.IsNullOrEmpty(
				instanceId))
			{
				throw new Exception(
					"Не найден SQL InstanceID");
			}

			string registryPath =
				@"SOFTWARE\Microsoft\Microsoft SQL Server\" +
				instanceId +
				@"\MSSQLServer";

			// Открываем 64-битный реестр
			using (var baseKey =
				RegistryKey.OpenBaseKey(
					RegistryHive.LocalMachine,
					RegistryView.Registry64))
			{
				using (var key =
					baseKey.OpenSubKey(
						registryPath,
						true))
				{
					if (key == null)
					{
						throw new Exception(
							"Не найден раздел SQL");
					}

					// 2 = Mixed mode
					key.SetValue(
						"LoginMode",
						2,
						RegistryValueKind.DWord);
				}
			}
		}

		/// <summary>
		/// Возвращает внутренний SQL Instance ID
		/// Например:
		/// SQLEXPRESS -> MSSQL17.SQLEXPRESS
		/// </summary>
		private string GetInstanceId(
			string instanceName)
		{
			// Сначала пробуем 64-битный реестр
			string result =
				GetInstanceIdInternal(
					instanceName,
					RegistryView.Registry64);

			if (!string.IsNullOrEmpty(
				result))
			{
				return result;
			}

			// Потом 32-битный
			return GetInstanceIdInternal(
				instanceName,
				RegistryView.Registry32);
		}

		/// <summary>
		/// Получение InstanceID
		/// из конкретного раздела реестра
		/// </summary>
		private string GetInstanceIdInternal(
			string instanceName,
			RegistryView view)
		{
			using (var baseKey =
				RegistryKey.OpenBaseKey(
					RegistryHive.LocalMachine,
					view))
			{
				using (var key =
					baseKey.OpenSubKey(
						@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"))
				{
					if (key == null)
					{
						return null;
					}

					object value =
						key.GetValue(
							instanceName);

					if (value == null)
					{
						return null;
					}

					return value.ToString();
				}
			}
		}

		
		public SqlInstanceInfo EnsureSqlInstalled()
		{
			// Ищем существующий SQL
			var sql =
				FindSqlExpress();

			if (sql.Exists)
			{
				return sql;
			}

			// Устанавливаем SQL
			InstallSqlExpress();

			// Ждём регистрацию SQL
			for (int i = 0; i < 30; i++)
			{
				System.Threading.Thread.Sleep(
					3000);

				sql =
					FindSqlExpress();

				if (sql.Exists)
				{
					return sql;
				}
			}

			throw new Exception(
				"SQL установлен, но экземпляр SQLEXPRESS не найден");
		}

		/// <summary>
		/// Тихая установка SQL Express 2019
		/// </summary>
		private void InstallSqlExpress()
		{
            
            // Каталог локального кэша SQL дистрибутива
            //string sqlDistribFolder =
            //    Path.Combine(
            //        AppDomain.CurrentDomain.BaseDirectory,
            //        "sql_distrib");

            string sqlDistribFolder =
                Path.Combine(ResourcePaths.WebInstallers, "sql_distrib");

            // Создаем каталог если его нет
            Directory.CreateDirectory(
                sqlDistribFolder);


            //        string sqlInstaller =
            //Path.Combine(
            //	sqlDistribFolder,
            //                "SQLEXPR_x64_RUS.exe");

            string sqlInstaller = 
                Path.Combine(
                    ResourcePaths.WebInstallers,
                    "sql_distrib",
                    "SQLEXPR_x64_RUS.exe");


            if (!File.Exists(sqlInstaller))
			{
				DownloadSqlMedia(
					sqlDistribFolder);
			}

			if (!File.Exists(sqlInstaller))
            {
                throw new Exception(
                    "После загрузки установщик SQL Server не найден.");
            }
            string extractFolder =
                Path.Combine(
                    sqlDistribFolder,
                    "SQLEXPR_x64_RUS");

            // Если папка уже существует — удалить полностью
            if (Directory.Exists(
                    extractFolder))
            {
                Directory.Delete(
                    extractFolder,
                    true);
            }

            // Создать заново пустую папку
            Directory.CreateDirectory(
                extractFolder);

            var extractProcess =
                new Process();

            extractProcess.StartInfo.FileName =
                sqlInstaller;

            extractProcess.StartInfo.Arguments =
                $"/x:\"{extractFolder}\" /q";

            extractProcess.StartInfo.UseShellExecute =
                true;

            extractProcess.StartInfo.Verb =
                "runas";

            extractProcess.Start();

            extractProcess.WaitForExit();

            if (extractProcess.ExitCode != 0)
            {
                throw new Exception(
                    "Ошибка распаковки SQL Server.\n\n" +
                    "Command:\n" +
                    extractProcess.StartInfo.FileName + " " +
                    extractProcess.StartInfo.Arguments +
                    "\n\nExitCode=" +
                    extractProcess.ExitCode);
            }

            var process =
				new Process();

            string setupPath =
				Path.Combine(
					sqlDistribFolder,
					"SQLEXPR_x64_RUS",
					"setup.exe");

            process.StartInfo.FileName =
                setupPath;


			process.StartInfo.Arguments =
                //"/QS " +
                "/Q " +
                "/ACTION=Install " +
				"/FEATURES=SQLEngine " +
				"/INSTANCENAME=SQLEXPRESS " +
				"/SQLSVCSTARTUPTYPE=Automatic " +
				"/TCPENABLED=1 " +

				// текущий пользователь Windows
				// станет SQL администратором
				"/ADDCURRENTUSERASSQLADMIN " +
				"/INDICATEPROGRESS " +
                "/UpdateEnabled=False " + // Не искать обновления экспериментально
                "/IACCEPTSQLSERVERLICENSETERMS";


			process.StartInfo.UseShellExecute =
				true;

			// Для UAC
			process.StartInfo.Verb =
				"runas";

			process.Start();

			process.WaitForExit();

            // ПОКАЗЫВАЕМ ТОЛЬКО ПРИ ОШИБКЕ
            if (process.ExitCode != 0 &&
				process.ExitCode != 3010) //3010 - требуется перезагрузка, но установка прошла успешно
            {
				MessageBox.Show(
					"Ошибка установки SQL!\n\n" +
					"ExitCode: " + process.ExitCode +
					"\n\nArguments:\n" + process.StartInfo.Arguments,
					"Ошибка установки SQL",
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);

				throw new Exception(
					"Ошибка установки SQL: " +
					process.ExitCode);
			}
						
		}

        private void DownloadSqlMedia(
			string sqlDistribFolder)
        {
            // web installer рядом с RRStoSQL.exe
            //string installerPath =
            //    Path.Combine(
            //        AppDomain.CurrentDomain.BaseDirectory,
            //        "web_installers",
            //        "SQL2022-SSEI-Expr.exe");

            string installerPath =
                Path.Combine(ResourcePaths.WebInstallers, "SQL2022-SSEI-Expr.exe");

            if (!File.Exists(installerPath))
            {
                throw new Exception(
                    "Не найден web installer SQL Server:\n\n" +
                    installerPath);
            }

            Process process =
                new Process();

            process.StartInfo.FileName =
                installerPath;

            //process.StartInfo.Arguments =
            //    "/ACTION=Download " +
            //    $"/MEDIAPATH=\"{sqlDistribFolder}\" " +
            //    "/QUIET";

            process.StartInfo.Arguments =
				"/Action=Download " +
				"/MediaType=Core " +
				$"/MediaPath=\"{sqlDistribFolder}\" " +
                //"/Passive"; ;
				"/Quiet"; ;


            process.StartInfo.UseShellExecute =
                true;

            process.StartInfo.Verb =
                "runas";

            process.Start();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    "Ошибка загрузки SQL Server Express.\n\n" +
                    "ExitCode=" +
                    process.ExitCode);
            }
        }

        /// <summary>
        /// Включает TCP и назначает порт 1433
        /// </summary>
        public void EnableTcp(
			string sqlInstance)
			{
		// SQLEXPRESS
		string instanceName =
			sqlInstance.Replace(
				@".\",
				"");

		string instanceId =
			GetInstanceId(
				instanceName);

		if (string.IsNullOrEmpty(
			instanceId))
		{
			throw new Exception(
				"Не найден SQL InstanceID");
		}

		string registryPath =
			@"SOFTWARE\Microsoft\Microsoft SQL Server\" +
			instanceId +
			@"\MSSQLServer\SuperSocketNetLib\Tcp\IPAll";

		using (RegistryKey baseKey =
			RegistryKey.OpenBaseKey(
				RegistryHive.LocalMachine,
				RegistryView.Registry64))

		using (RegistryKey key =
			baseKey.OpenSubKey(
				registryPath,
				true))
		{
			if (key == null)
			{
				throw new Exception(
					"Не найден раздел TCP");
			}

			// отключаем динамические порты
			key.SetValue(
				"TcpDynamicPorts",
				"");

			// фиксированный порт
			key.SetValue(
				"TcpPort",
				"1433");
		}
	}

	
/// <summary>
/// Открывает порт 1433
/// в Windows Firewall
/// </summary>
public void OpenFirewallPort()
	{
		Process process =
			new Process();

		process.StartInfo.FileName =
			"netsh";

		process.StartInfo.Arguments =
			"advfirewall firewall add rule " +
			"name=\"SQL Server 1433\" " +
			"dir=in " +
			"action=allow " +
			"protocol=TCP " +
			"localport=1433";

		process.StartInfo.UseShellExecute =
			false;

		process.StartInfo.CreateNoWindow =
			true;

		process.Start();

		process.WaitForExit();

		// Код 0 = успешно
		if (process.ExitCode != 0)
		{
			throw new Exception(
				"Не удалось открыть порт 1433");
		}
	}

		/// <summary>
		/// Выполняет все SQL patch-файлы
		/// из папки SQL_patches
		/// </summary>
		public void ExecuteSqlPatches(
			string sqlInstance,
			string databaseName)
		{
			// Папка с exe
			string appFolder =
				AppDomain.CurrentDomain.BaseDirectory;

			// SQL_patches
			string patchesFolder =
				Path.Combine(
					appFolder,
					"SQL_patches");

			// Если папки нет
			if (!Directory.Exists(
				patchesFolder))
			{
				return;
			}

			// Получаем все *.sql
			string[] files =
				Directory.GetFiles(
					patchesFolder,
					"*.sql");

			// Стабильный порядок выполнения
			files =
				files
				.OrderBy(
					x => x)
				.ToArray();

			string connectionString =
				$"Server={sqlInstance};" +
				$"Database={databaseName};" +
				$"Integrated Security=True;" +
				$"TrustServerCertificate=True;";

			using (var connection =
				new SqlConnection(
					connectionString))
			{
				connection.Open();

				foreach (string file
					in files)
				{
					string sql =
						File.ReadAllText(
							file);
					string fileName =
						Path.GetFileName(
							file);

					// Пустой файл
					if (string.IsNullOrWhiteSpace(
						sql))
					{
						continue;
					}

					try
					{
						using (var command =
							new SqlCommand(
								sql,
								connection))
						{
							command.CommandTimeout =
								600;

							command.ExecuteNonQuery();
						}
					}
					catch (Exception ex)
					{
						throw new Exception(
							$"Ошибка SQL patch:\n\n" +
							$"{fileName}\n\n" +
							ex.ToString());
					}
				}
			}
		}

        /// <summary>
		///  Проверяет если база данных с таким именем уже существует и возвращает доступное имя базы данных + _1, _2 и т.д.
		/// </summary>
		
        public string GetAvailableDatabaseName(
			string sqlInstance,
			string webServiceLogin)
        {

            //УР новая и webServiceLogin еще нет в конфиге назовем базу RRS_Db
            string login =
				string.IsNullOrWhiteSpace(webServiceLogin)
					? "Db"
					: webServiceLogin.Trim();

            string baseName =
                $"RRS_{login}";


            //string baseName =
            //    $"RRS_{webServiceLogin}";


            string databaseName =
                baseName;

            int index = 1;

            while (DatabaseExists(
                sqlInstance,
                databaseName))
            {
                databaseName =
                    $"{baseName}_{index}";

                index++;
            }

            return databaseName;
        }

        //вспомогательная функция для проверки существования базы данных
        private bool DatabaseExists(
			string sqlInstance,
			string databaseName)
        {
            string connectionString =
                $"Server={sqlInstance};" +
                "Database=master;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True;";

            using (var connection =
                new SqlConnection(
                    connectionString))
            {
                connection.Open();

                using (var command =
                    new SqlCommand(
                        @"SELECT COUNT(*)
                  FROM sys.databases
                  WHERE name = @name",
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "@name",
                        databaseName);

                    return (int)command.ExecuteScalar() > 0;
                }
            }
        }

        // ============================================
        // Возвращает папку DATA экземпляра SQL Server.
        // Например:
        //
        // C:\Program Files\Microsoft SQL Server\
        // MSSQL16.SQLEXPRESS\MSSQL\DATA
        // ============================================
        public string GetDefaultDataFolder(
            string sqlInstance)
        {
            string connectionString =
                $"Server={sqlInstance};" +
                $"Database=master;" +
                $"Trusted_Connection=True;";

            using (var connection =
                new SqlConnection(connectionString))
            {
                connection.Open();

                // ============================================
                // SQL Server 2017+
                // ============================================

                try
                {
                    using (var command =
                        new SqlCommand(
                            @"SELECT CAST(
                        SERVERPROPERTY('InstanceDefaultDataPath')
                        AS nvarchar(4000))",
                            connection))
                    {
                        var result =
                            command.ExecuteScalar();

                        if (result != null &&
                            result != DBNull.Value)
                        {
                            string path =
                                result.ToString();

                            if (!string.IsNullOrWhiteSpace(path) &&
                                Directory.Exists(path))
                            {
                                return path;
                            }
                        }
                    }
                }
                catch
                {
                    // попробуем старый способ
                }

                // ============================================
                // Старые версии SQL Server
                // ============================================

                using (var command =
                    new SqlCommand(
                        @"SELECT physical_name
                  FROM master.sys.database_files
                  WHERE file_id = 1",
                        connection))
                {
                    var result =
                        command.ExecuteScalar();

                    if (result == null ||
                        result == DBNull.Value)
                    {
                        throw new Exception(
                            "Не удалось определить каталог DATA SQL Server.");
                    }

                    return Path.GetDirectoryName(
                        result.ToString());
                }
            }
        }

        private void DetachDatabase(
			string sqlInstance,
			string databaseName)
        {
            string connectionString =
                $"Server={sqlInstance};" +
                $"Database=master;" +
                $"Trusted_Connection=True;";

            using (var connection =
                new SqlConnection(connectionString))
            {
                connection.Open();

                string sql =
        $@"
ALTER DATABASE [{databaseName}]
SET SINGLE_USER
WITH ROLLBACK IMMEDIATE;

EXEC sp_detach_db
    @dbname = N'{databaseName}';
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

        private void AttachDatabase(
            string sqlInstance,
			string databaseName,
			string mdfPath,
			string ldfPath)
        {
            string connectionString =
                $"Server={sqlInstance};" +
                $"Database=master;" +
                $"Trusted_Connection=True;";

            using (var connection =
                new SqlConnection(connectionString))
            {
                connection.Open();

                string sql =
        $@"
CREATE DATABASE [{databaseName}]
ON
(
    FILENAME = N'{mdfPath}'
),
(
    FILENAME = N'{ldfPath}'
)
FOR ATTACH;
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

        public void DropDatabaseIfExists(
    string sqlInstance,
    string databaseName)
        {
            if (!DatabaseExists(
                    sqlInstance,
                    databaseName))
            {
                return;
            }

            DetachDatabase(
                sqlInstance,
                databaseName);
        }


        public void CreateDatabaseFromTemplate(
			string sqlInstance,
			string databaseName,
			string templateFolder)
        {
            string dataFolder =
                GetDefaultDataFolder(
                    sqlInstance);

            string sourceMdf =
                Path.Combine(
                    templateFolder,
                    "citisoftDbSlis.mdf");

            string sourceLdf =
                Path.Combine(
                    templateFolder,
                    "citisoftDbSlis_log.ldf");

            if (!File.Exists(sourceMdf))
                throw new FileNotFoundException(sourceMdf);

            if (!File.Exists(sourceLdf))
                throw new FileNotFoundException(sourceLdf);

            string targetMdf =
                Path.Combine(
                    dataFolder,
                    databaseName + ".mdf");

            string targetLdf =
                Path.Combine(
                    dataFolder,
                    databaseName + "_log.ldf");

            if (DatabaseExists(
                    sqlInstance,
                    databaseName))
            {
                DetachDatabase(
                    sqlInstance,
                    databaseName);
            }

            if (File.Exists(targetMdf))
                File.Delete(targetMdf);

            if (File.Exists(targetLdf))
                File.Delete(targetLdf);

            File.Copy(
                sourceMdf,
                targetMdf);

            File.Copy(
                sourceLdf,
                targetLdf);

            AttachDatabase(
                sqlInstance,
                databaseName,
                targetMdf,
                targetLdf);
        }

        //    public string GetDatabaseVersion(
        //string sqlInstance,
        //string databaseName)
        //    {
        //        string connectionString =
        //            $"Server={sqlInstance};" +
        //            $"Database={databaseName};" +
        //            $"Trusted_Connection=True;";

        //        using (var connection =
        //            new SqlConnection(connectionString))
        //        {
        //            connection.Open();

        //            using (var command =
        //                new SqlCommand(
        //                    @"SELECT Value
        //              FROM dbo.SysParameter
        //              WHERE Name = 'DbVersion'",
        //                    connection))
        //            {
        //                object value =
        //                    command.ExecuteScalar();

        //                if (value == null ||
        //                    value == DBNull.Value)
        //                {
        //                    throw new Exception(
        //                        "Параметр DbVersion не найден.");
        //                }

        //                return value.ToString();
        //            }
        //        }
        //    }

        public string GetDatabaseVersion(
    string sqlInstance,
    string databaseName)
        {
            string connectionString =
                $"Server={sqlInstance};" +
                $"Database={databaseName};" +
                $"Trusted_Connection=True;";

            using (var connection =
                new SqlConnection(connectionString))
            {
                connection.Open();

                try
                {
                    using (var command =
                        new SqlCommand(
                            @"SELECT Value
                      FROM dbo.SysParameter
                      WHERE Name='DbVersion'",
                            connection))
                    {
                        object value =
                            command.ExecuteScalar();

                        if (value == null ||
                            value == DBNull.Value)
                        {
                            return null;
                        }

                        return value.ToString();
                    }
                }
                catch (SqlException ex)
                    when (ex.Number == 208)   // Invalid object name
                {
                    // Таблица SysParameter ещё не создана.
                    return null;
                }
            }
        }


        public void WaitForDatabaseUpgrade(
    string sqlInstance,
    string databaseName,
    int stableSeconds = 5,
    int timeoutSeconds = 180)
        {
            DateTime start =
                DateTime.Now;

            string lastVersion =
                GetDatabaseVersion(
                    sqlInstance,
                    databaseName);

            DateTime lastChange =
                DateTime.Now;

            while (true)
            {
                string currentVersion =
                    GetDatabaseVersion(
                        sqlInstance,
                        databaseName);

                if (!String.Equals(
                        currentVersion,
                        lastVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    lastVersion =
                        currentVersion;

                    lastChange =
                        DateTime.Now;
                }

                // Версия не менялась несколько секунд —
                // считаем обновление завершённым.
                if ((DateTime.Now - lastChange).TotalSeconds >=
                    stableSeconds)
                {
                    return;
                }

                if ((DateTime.Now - start).TotalSeconds >
                    timeoutSeconds)
                {
                    throw new System.TimeoutException(
                        "Истекло время ожидания обновления структуры базы.");
                }

                Thread.Sleep(1000);
            }
        }






    }
}