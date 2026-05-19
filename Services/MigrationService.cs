using RRStoSQL.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RRStoSQL.Services
{
	public class MigrationService
	{
		public async Task RunAsync(
			Action<string, StepStatus> updateStep)
		{
			var steps = GetSteps();

			foreach (var step in steps)
			{
				updateStep(
					step.Name,
					StepStatus.Running);

				try
				{
					await ExecuteStep(step);

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