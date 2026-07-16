using RRStoSQL.Forms;
using RRStoSQL.Helpers;
using RRStoSQL.Models;
using RRStoSQL.Services.Migration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace RRStoSQL.Services
{
	/// <summary>
	/// Выполняет последовательность шагов миграции
	/// Access -> SQL Server.
	/// </summary>
	public class MigrationService
	{
		
		private readonly ConfigService _configService = new ConfigService();
		
        private readonly AccessBaseService _accessBaseService = new AccessBaseService();
        
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
            Form owner,
            string configPath,
            MigrationOptions options,
            Action<string, StepStatus> updateStep)
        {
            
            // Объект с параметрами текущей миграции
            AppConfig appConfig = null;

            //Объект с параметрами текущей миграции
            appConfig =
               _configService.Read(
                   configPath);

            //MessageBox.Show("");
            // Проверяем, не запущен ли УР
            if (IsRemoteRegRunning(
                    appConfig))
            {
                MessageBox.Show(
                    owner,
                    "Закройте УР " +
                    "и не открывайте ее до завершения миграции.",
                    "УР запущена",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            //Заменяем новую базу базой с правильной структурой и версией БД из папки \misc\Data.mdb
            _accessBaseService.ReplaceIfNewDatabase(
                appConfig.MdbPath);

            //Создаем пайплайн миграции
            var pipeline =
                MigrationPipelineFactory.Create();

            var context =
                new MigrationContext
                {
                    Owner = owner,
                    ConfigPath = configPath
                };
            //перебираем все шаги в пайлайне и выполняем их по очереди, если они должны быть выполнены согласно опциям миграции
            foreach (IMigrationStep step in pipeline.Steps)
            {

                if (!ShouldRunStep(step, options))
                {
                    continue;
                }

                updateStep(
                    step.Name,
                    StepStatus.Running);

                try
                {
                    await step.ExecuteAsync(
                        context);

                    //appConfig =
                    //    context.Config;

                    updateStep(
                        step.Name,
                        StepStatus.Success);
                }
                catch (Exception ex)
                {
                    updateStep(
                        step.Name,
                        StepStatus.Error);

                    MessageHelper.Show(
                        ex.ToString(),
                        "Ошибка");

                    if (step.Critical)
                    {
                        return;
                    }
                }
            }

        }

        private bool ShouldRunStep(
            IMigrationStep step,
            MigrationOptions options)
        {
            switch (step.Name)
            {
                case "Установка sql management":
                    return options.InstallSsms;

                case "Удаляем bullzip":
                    return options.DeleteBullzip;

                default:
                    return true;
            }
        }
        private bool IsRemoteRegRunning(
			AppConfig appConfig)
				{
					string expectedPath =
						Path.GetFullPath(
							appConfig.ExePath);

					foreach (var process in
						Process.GetProcessesByName(
							"RemoteReg"))
					{
						try
						{
							if (string.Equals(
								process.MainModule.FileName,
								expectedPath,
								StringComparison.OrdinalIgnoreCase))
							{
								return true;
							}
						}
						catch
						{
						}
					}

					return false;
				}


    }
}