using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using RRStoSQL.Forms;
using RRStoSQL.Helpers;
using RRStoSQL.Models;

//using System.Diagnostics;
//using System.Threading;

namespace RRStoSQL.Services
{
	/// <summary>
	/// Выполняет последовательность шагов миграции
	/// Access -> SQL Server.
	/// </summary>
	public class MigrationService
	{
		/// <summary>
		/// Работа с RemoteReg.exe.config
		/// </summary>
		private readonly ConfigService _configService = new ConfigService();
		/// <summary>
		/// Работа с SQL Server
		/// </summary>
		private readonly SqlService _sqlService = new SqlService();
	//	private readonly AccessToSqlConverter _converter = new AccessToSqlConverter();
		private readonly AccessConverterService _converter = new AccessConverterService();

		/// <summary>
		/// Запускает миграцию.
		/// </summary>
		/// <param name="configPath">
		/// Полный путь к выбранному
		/// RemoteReg.exe.config
		/// </param>
		/// <param name="updateStep">
		/// Делегат обновления статуса шага
		/// </param>
		public async Task RunAsync(
			string configPath,
			Action<string, StepStatus> updateStep)
		{
			// Получаем список шагов
			var steps = GetSteps();

			// Объект с параметрами текущей миграции
			AppConfig appConfig = null;

			foreach (var step in steps)
			{
				// Устанавливаем статус:
				// "выполняется"
				updateStep(
					step.Name,
					StepStatus.Running);

				try
				{
					// ============================================
					// Читаем конфиг
					// ============================================

					if (step.Name == "Читаем конфиг")
					{
						// Читаем настройки приложения
						appConfig =
							_configService.Read(
								configPath);

						// SQL по умолчанию
						appConfig.SqlInstance =
							@".\SQLEXPRESS";

						// Генерируем имя новой базы
						appConfig.SqlDatabaseName =
							$"RRS_{DateTime.Now:yyyyMMdd_HHmmss}";

						// Временно выводим отладочную информацию
						//MessageBox.Show(
						//	$"MDB:\n{appConfig.MdbPath}\n\n" +
						//	$"EXE:\n{appConfig.ExePath}\n\n" +
						//	$"DB:\n{appConfig.SqlDatabaseName}");

						// Проверяем существование MDB
						if (!File.Exists(
							appConfig.MdbPath))
						{
							throw new Exception(
								"MDB не найден");
						}
					}

					// ============================================
					// Проверка/установка SQL
					// ============================================

					else if (step.Name == "Установка SQL")
					{
						var sql =
							_sqlService
							.EnsureSqlInstalled();

						appConfig.SqlInstance =
							sql.InstanceName;

						// Временно выводим найденный SQL
						MessageBox.Show(
							$"Конвертация будет выполнена в экземпляр :\n" +
							$"{appConfig.SqlInstance}");
					}




					// ============================================
					// Разрешаем mixed authentication
					// ============================================

					else if (step.Name == "Разрешаем mixed authentication")
					{
						_sqlService
							.EnableMixedAuthentication(
								appConfig.SqlInstance);
					}

					// ============================================
					// Перезапуск службы SQL
					// ============================================

					else if (
						step.Name ==
						"Перезапуск службы SQL")
					{
						_sqlService
							.RestartSqlService(
								appConfig.SqlInstance);
					}



					// ============================================
					// Создаём RRUser
					// ============================================

					else if (
						step.Name ==
						"Создаём RRUser")
					{
						_sqlService
							.CreateRRUser(
								appConfig.SqlInstance);
					}


					// ============================================
					// Конвертируем базу
					// ============================================

					else if (
						step.Name ==
						"Конвертируем базу")
					{
						// запускаем Bullzip
						Process process =
							_converter.Convert(
								appConfig);

	//					MessageBox.Show(
	//"Лог:\n\n" +
	//_converter.GetLogFilePath());
	//					// показываем окно лога
						using (
							var logForm =
								new ConversionLogForm(
									_converter.GetLogFilePath(),
									process))
						{
							logForm.ShowDialog();
						}

						// проверяем результат
						if (!process.HasExited)
						{
							process.WaitForExit();
						}

						if (process.ExitCode != 0)
						{
							throw new Exception(
								"Ошибка конвертации Bullzip.\n\n" +
								"Код завершения: " +
								process.ExitCode);
						}
					}


					// ============================================
					// Выдаём права RRUser
					// ============================================

					else if (
						step.Name ==
						"Выдаём права RRUser")
					{
						_sqlService
							.GrantDatabaseRights(
								appConfig.SqlInstance,
								appConfig.SqlDatabaseName);
					}
					// ============================================
					// Разрешаем TCP 1433 SQL. Назначаем порт 1433 для SQL
					// ============================================

					else if (
						step.Name ==
						"Назначаем порт 1433 для SQL")
					{
						_sqlService.EnableTcp(
							appConfig.SqlInstance);

						// настройки TCP применяются
						// только после рестарта службы
						_sqlService.RestartSqlService(
							appConfig.SqlInstance);
					}

					// ============================================
					// Открываем порт 1433 в брандмауэре Windows
					// ============================================

					else if (
						step.Name ==
						"Открываем 1433 в брандмауэре")
					{
						_sqlService
							.OpenFirewallPort();
					}


					// ============================================
					// Меняем connectionStrings
					// ============================================

					else if (
						step.Name ==
						"Меняем connectionStrings")
					{
						_configService.SwitchToSql(
							configPath,
							appConfig);
					}


					// ============================================
					// Строка подключения в буфер обмена
					// ============================================

					else if (
						step.Name ==
						"Строка подключения в буфер обмена")
					{
						_configService
							.CopyConnectionString(
								appConfig);
					}

					// ============================================
					// Запускаем УР
					// ============================================

					else if (
						step.Name ==
						"Запускаем УР")
					{
						StartRemoteReg(
							appConfig);
					}

					else
					{
						await ExecuteStep(step);
					}
									

					// Шаг успешно завершён
					updateStep(
						step.Name,
						StepStatus.Success);
				}
				catch (Exception ex)
				{
					// Помечаем шаг ошибкой
					updateStep(
						step.Name,
						StepStatus.Error);

					// Показываем нормальное окно
					MessageHelper.Show(
						ex.ToString(),
						"Ошибка");

					return;
				}
				//catch
				//{
				//	// Помечаем шаг ошибкой
				//	updateStep(
				//		step.Name,
				//		StepStatus.Error);

				//	throw;
				//}
			}
		}

		/// <summary>
		/// Временная заглушка для
		/// ещё не реализованных шагов.
		/// </summary>
		private async Task ExecuteStep(
			MigrationStep step)
		{
			await Task.Delay(1000);

			// Потом здесь будут:
			//
			// EnableMixedAuthentication()
			// CreateSqlUser()
			// RestartSqlService()
			// ConvertAccessDatabase()
			// EnableTcp1433()
			// OpenFirewallPort()
			// UpdateConnectionStrings()
			// ExecuteScripts()
			// LaunchRemoteReg()
		}

		/// <summary>
		/// Возвращает список шагов миграции.
		/// Порядок важен.
		/// </summary>
		private List<MigrationStep> GetSteps()
		{
			return new List<MigrationStep>
	{
		new MigrationStep("Читаем конфиг"),
		new MigrationStep("Установка SQL"),
		new MigrationStep("Разрешаем mixed authentication"),
		new MigrationStep("Перезапуск службы SQL"),
		new MigrationStep("Создаём RRUser"),		
		new MigrationStep("Конвертируем базу"),
		new MigrationStep("Выдаём права RRUser"),
		new MigrationStep("Назначаем порт 1433 для SQL"),
		new MigrationStep("Открываем 1433 в брандмауэре"),
		new MigrationStep("Меняем connectionStrings"),
		new MigrationStep("Выполняем патчи"),
		new MigrationStep("Строка подключения в буфер обмена"),
		new MigrationStep("Запускаем УР")
	};
		}

		/// <summary>
		/// Запускает RemoteReg.exe
		/// </summary>
		private void StartRemoteReg(
			AppConfig appConfig)
		{
			if (!File.Exists(
				appConfig.ExePath))
			{
				throw new Exception(
					"RemoteReg.exe не найден");
			}

			Process.Start(
				appConfig.ExePath);
		}
	}
}