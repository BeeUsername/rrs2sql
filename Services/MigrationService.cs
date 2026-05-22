using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using RRStoSQL.Models;
//using RRStoSQL.Models;

namespace RRStoSQL.Services
{
	public class MigrationService
	{

		private readonly ConfigService
	_configService =
		new ConfigService();

		//public async Task RunAsync(
		//			Action<string, StepStatus> updateStep)
		public async Task RunAsync(
	string configPath,
	Action<string, StepStatus> updateStep)
		{
			var steps = GetSteps();
			AppConfig appConfig = null;

			foreach (var step in steps)
			{
				updateStep(
					step.Name,
					StepStatus.Running);

				try
				{
					//	await ExecuteStep(step);
					if (step.Name == "Читаем конфиг")
					{
						appConfig =
							_configService.Read(
								configPath);

						appConfig.SqlInstance =
							@".\SQLEXPRESS";

						appConfig.SqlDatabaseName =
							$"RRS_{DateTime.Now:yyyyMMdd_HHmmss}";

						MessageBox.Show(
$"MDB:\n{appConfig.MdbPath}\n\n" +
$"EXE:\n{appConfig.ExePath}\n\n" +
$"DB:\n{appConfig.SqlDatabaseName}");

						if (!File.Exists(
							appConfig.MdbPath))
						{
							throw new Exception(
								"MDB не найден");
						}
					}
					else
					{
						await ExecuteStep(step);
					}

					updateStep(
						step.Name,
						StepStatus.Success);
				}
				catch
				{
					updateStep(
						step.Name,
						StepStatus.Error);

					throw;
				}
			}
		}

		private async Task ExecuteStep(
			MigrationStep step)
		{
			await Task.Delay(1000);

			// потом здесь будет:
			// ReadConfig()
			// InstallSql()
			// ConvertAccess()
			// UpdateConfig()
		}

		private List<MigrationStep> GetSteps()
		{
			return new List<MigrationStep>
			{
				new MigrationStep("Читаем конфиг"),
				new MigrationStep("Установка SQL"),
				new MigrationStep("Разрешаем mixed authentication"),
				new MigrationStep("Создаём RRUser"),
				new MigrationStep("Перезапуск службы SQL"),
				new MigrationStep("Конвертируем базу"),
				new MigrationStep("Разрешаем TCP 1433"),
				new MigrationStep("Открываем порт 1433"),
				new MigrationStep("Меняем connectionStrings"),
				new MigrationStep("Выполняем патчи"),
				new MigrationStep("Копируем строку подключения"),
				new MigrationStep("Запускаем УР")
			};
		}
	}
}