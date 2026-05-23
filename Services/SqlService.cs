using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using RRStoSQL.Models;


namespace RRStoSQL.Services
{
	/// <summary>
	/// Работа с SQL Server
	/// </summary>
	public class SqlService
	{
		/// <summary>
		/// Ищет установленный SQLEXPRESS
		/// </summary>
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
		/// Получает внутренний SQL ID
		/// например:
		/// SQLEXPRESS -> MSSQL16.SQLEXPRESS
		/// </summary>
		private string GetInstanceId(
			string instanceName)
		{
			string path =
				@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";

			using (RegistryKey key =
				Registry.LocalMachine.OpenSubKey(
					path))
			{
				if (key == null)
				{
					return null;
				}

				return key.GetValue(
					instanceName)
					?.ToString();
			}
		}

		/// <summary>
		/// Перезапуск службы SQL
		/// </summary>
		public void RestartSqlService(
			string sqlInstance)
		{
			string serviceName =
				"MSSQL$" +
				sqlInstance.Replace(@".\", "");

			using (var service =
				new ServiceController(
					serviceName))
			{
				if (service.Status ==
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
		/// Проверяет наличие SQL.
		/// Если SQL отсутствует — устанавливает.
		/// </summary>
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

	}
}