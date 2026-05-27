using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using RRStoSQL.Models;
using System.Data.SqlClient;


//using System.ServiceProcess;



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
			// .\SQLEXPRESS → SQLEXPRESS
			string serviceName =
				"MSSQL$" +
				sqlInstance.Replace(
					@".\",
					"");

			using (var service =
				new ServiceController(
					serviceName))
			{
				if (
					service.Status ==
					ServiceControllerStatus.Running)
				{
					service.Stop();

					service.WaitForStatus(
						ServiceControllerStatus.Stopped,
						TimeSpan.FromMinutes(1));
				}

				service.Start();

				service.WaitForStatus(
					ServiceControllerStatus.Running,
					TimeSpan.FromMinutes(1));
			}
		}

		/// <summary>
		/// Создаёт логин RRUser и пользователя
		/// </summary>
		public void CreateRRUser(
			string sqlInstance)
		{
			// Подключаемся через Windows Authentication
			// т.к. RRUser ещё не существует
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
		@"
IF NOT EXISTS(
    SELECT *
    FROM sys.server_principals
    WHERE name='RRUser'
)
BEGIN

    CREATE LOGIN RRUser
    WITH PASSWORD='RRS_999999999!';

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

			MessageBox.Show(
	"InstanceId:\n" +
	instanceId);


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
			// Папка лежит рядом с нашей утилитой
			string setupPath =
	Path.Combine(
		AppDomain.CurrentDomain.BaseDirectory,
		"Sql_distrib",
		"setup.exe");

			if (!File.Exists(setupPath))
			{
				throw new Exception(
					"Не найден Sql_distrib\\setup.exe");
			}

			var process =
				new Process();

			process.StartInfo.FileName =
				setupPath;

			process.StartInfo.Arguments =
				"/Q " +
				"/ACTION=Install " +
				"/FEATURES=SQLEngine " +
				"/INSTANCENAME=SQLEXPRESS " +
				"/SQLSVCSTARTUPTYPE=Automatic " +
				"/TCPENABLED=1 " +

				// текущий пользователь Windows
				// станет SQL администратором
				"/ADDCURRENTUSERASSQLADMIN " +

				"/IACCEPTSQLSERVERLICENSETERMS";

			process.StartInfo.UseShellExecute =
				true;

			// Для UAC
			process.StartInfo.Verb =
				"runas";

			process.Start();

			process.WaitForExit();

			MessageBox.Show(
				"ExitCode: " +
				process.ExitCode +
				"\n\nArguments:\n" +
				process.StartInfo.Arguments);

			if (process.ExitCode != 0)
			{
				throw new Exception(
					"Ошибка установки SQL: " +
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

}
}