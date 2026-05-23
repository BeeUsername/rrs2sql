using RRStoSQL.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

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
		private readonly ConfigService
			_configService =
				new ConfigService();

		/// <summary>
		/// Работа с SQL Server
		/// </summary>
		private readonly SqlService
			_sqlService =
				new SqlService();

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
						MessageBox.Show(
							$"MDB:\n{appConfig.MdbPath}\n\n" +
							$"EXE:\n{appConfig.ExePath}\n\n" +
							$"DB:\n{appConfig.SqlDatabaseName}");

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
							$"SQL найден:\n" +
							$"{appConfig.SqlInstance}");
					}


					// ============================================
					// Остальные шаги пока заглушки
					// ============================================

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

					else if (step.Name == "Перезапуск службы SQL")
					{
						_sqlService
							.RestartSqlService(
								appConfig.SqlInstance);
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
				catch
				{
					// Помечаем шаг ошибкой
					updateStep(
						step.Name,
						StepStatus.Error);

					throw;
				}
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
				new MigrationStep(
					"Читаем конфиг"),

				new MigrationStep(
					"Установка SQL"),

				new MigrationStep(
					"Разрешаем mixed authentication"),

				new MigrationStep(
					"Создаём RRUser"),

				new MigrationStep(
					"Перезапуск службы SQL"),

				new MigrationStep(
					"Конвертируем базу"),

				new MigrationStep(
					"Разрешаем TCP 1433"),

				new MigrationStep(
					"Открываем порт 1433"),

				new MigrationStep(
					"Меняем connectionStrings"),

				new MigrationStep(
					"Выполняем патчи"),

				new MigrationStep(
					"Копируем строку подключения"),

				new MigrationStep(
					"Запускаем УР")
			};
		}
	}
}