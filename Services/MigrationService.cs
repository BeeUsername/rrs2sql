using RRStoSQL.Forms;
using RRStoSQL.Helpers;
using RRStoSQL.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
		private readonly AccessConverterService _converter = new AccessConverterService();
		private readonly SqlPatchService _patchService = new SqlPatchService();
		private readonly BullzipService _bullzipService = new BullzipService();
		private readonly AccessPatchService _accessPatchService = new AccessPatchService();
        private readonly SsmsService _ssmsService = new SsmsService();

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
			// Получаем список шагов
			var steps = GetSteps(options);

			// Объект с параметрами текущей миграции
			AppConfig appConfig = null;

			//_ssmsService.EnsureInstalled();
			//return;

			//Объект с параметрами текущей миграции
		    appConfig =
			   _configService.Read(
				   configPath);

			// Проверяем, не запущен ли УР
			if (IsRemoteRegRunning(
					appConfig))
			{
				MessageBox.Show(
					owner,
					"Закройте УР" +
					"и не открывайте ее до завершения миграции.",
					"УР запущен",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);

				return;
			}

            //Запускаем шаги миграции

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

                        // Формируем уникальное имя SQL базы
                        // на основании WebServiceLogin
                        appConfig.SqlDatabaseName =
                            _sqlService.GetAvailableDatabaseName(
                                appConfig.SqlInstance,
                                appConfig.WebServiceLogin);
												
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

                        //return;

                    }

					// ============================================
					// Установка Bullzip msa2sql
					// ============================================

					else if (
						step.Name ==
						"Установка Bullzip msa2sql")
					{
						string exePath =
							_bullzipService
								.EnsureInstalled();
												
					}


					// ============================================
					// Разрешаем mixed authentication
					// ============================================

					else if (step.Name == "Разрешаем Mixed authentication")
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
					//  Патчим Access базу
					// ============================================

					else if (
						step.Name ==
						"Патчим Access базу")
					{
						_accessPatchService
							.ExecutePatches(
								appConfig.MdbPath);
						
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
                        
						// Запускаем форму с логом конвертации
                        using (var logForm =
							new ConversionLogForm(
								owner,
								_converter.GetLogFilePath()))
                        {
                            var result = logForm.ShowDialog(owner);

                            if (result == DialogResult.Abort)
                            {
                                _bullzipService.CopyBullzipLog();
                                throw new Exception(
                                    "Конвертация Bullzip завершилась с ошибкой.");
                            }
                        }
                        
                        //проверяем результат
                        if (!process.HasExited)
						{
							process.WaitForExit();
						}

						try
						{
							_bullzipService.CopyBullzipLog();
						}
						catch
						{
							// лог не должен ломать миграцию
						}
						
						if (process.ExitCode != 0)
						{
							//_bullzipService.CopyBullzipLog();
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
						"Меняем ConnectionStrings")
					{
						_configService.SwitchToSql(
							configPath,
							appConfig);
					}
					// ============================================
					// Патчим SQL базу
					// ============================================

					else if (step.Name =="Патчим SQL базу")
					{
						_patchService.ExecutePatches(
							appConfig.SqlInstance,
							appConfig.SqlDatabaseName);
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

                    // ============================================
                    // Установка SQL Managment
                    // ============================================

                    else if (
                        step.Name ==
                        "Установка SQL Management")
                    {
                        _ssmsService.EnsureInstalled();
                    }


                    // ============================================
                    // Удаляем Bullzip
                    // ============================================


                    else if (
						step.Name ==
						"Удаляем Bullzip")
                    {
                        _bullzipService.Uninstall();
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
					updateStep(
						step.Name,
						StepStatus.Error);

					MessageHelper.Show(
						ex.ToString(),
						"Ошибка");

					if (IsCriticalStep(step.Name))
					{
						return;
					}

					// идём к следующему шагу
					continue;
				}
				
			}
		}

        //список этапов ошибка в которых приводит к остановке миграции
        private bool IsCriticalStep(
			string stepName)
		{
			switch (stepName)
			{
				case "Читаем конфиг":
				case "Установка SQL":
				//case "Разрешаем mixed authentication":
				case "Перезапуск службы SQL":
				case "Создаём RRUser":
				case "Конвертируем базу":
				//case "Выдаём права RRUser":
					return true;

				default:
					return false;
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
			
		}

        /// <summary>
        /// Возвращает список шагов миграции.
        /// Порядок важен.
        /// </summary>
        //private List<MigrationStep> GetSteps()
        private List<MigrationStep> GetSteps(
			 MigrationOptions options)
        {
            var steps =
            new List<MigrationStep>
            {
        new MigrationStep("Читаем конфиг"),
		new MigrationStep("Установка SQL"),
		new MigrationStep("Установка Bullzip msa2sql"),
		new MigrationStep("Разрешаем Mixed authentication"),
		new MigrationStep("Перезапуск службы SQL"),
		new MigrationStep("Создаём RRUser"),
		new MigrationStep("Патчим Access базу"),
		new MigrationStep("Конвертируем базу"),
		new MigrationStep("Выдаём права RRUser"),
		new MigrationStep("Назначаем порт 1433 для SQL"),
		new MigrationStep("Открываем 1433 в брандмауэре"),
		new MigrationStep("Меняем ConnectionStrings"),
		new MigrationStep("Патчим SQL базу"),
		new MigrationStep("Строка подключения в буфер обмена"),
		new MigrationStep("Запускаем УР"),
		};

            if (options.InstallSsms)
            {
                steps.Add(
                    new MigrationStep(
                        "Установка SQL Management"));
            }

            if (options.DeleteBullzip)
            {
                steps.Add(
                    new MigrationStep(
                        "Удаляем Bullzip"));
            }

            return steps;
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

            //Process.Start(
            //	appConfig.ExePath);

            var process =
			 new Process();

            process.StartInfo.FileName =
                appConfig.ExePath;

            process.StartInfo.WorkingDirectory =
                appConfig.AppFolder;

            process.Start();
        }

        //Проверка запущен ли RemoteReg.exe, вызывается перед конвертацией базы
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