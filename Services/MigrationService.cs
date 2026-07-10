using RRStoSQL.Forms;
using RRStoSQL.Helpers;
using RRStoSQL.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        private readonly AccessCompactService _accessCompactService = new AccessCompactService();
        private readonly AccessCorruptedRecordsService _corruptedRecordsService = new AccessCorruptedRecordsService();
        private readonly SqlTemplateDatabaseService _templateDatabaseService = new SqlTemplateDatabaseService();
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
					"УР запущена",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);

				return;
			}

            

            //Заменяем новую базу базой с правильной структурой
            _accessBaseService.ReplaceIfNewDatabase(
				appConfig.MdbPath);

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

						
						// Проверяем существование MDB
						if (!File.Exists(
							appConfig.MdbPath))
						{
							throw new Exception(
								"MDB не найден");
						}
					}

					// ============================================
					// Сжимаем базу Access
					// ============================================

					else if (
						step.Name ==
						"Сжимаем базу")
					{
						//continue;
						_accessCompactService.Compact(
							appConfig.MdbPath);

						//иногда Windows еще несколько десятков миллисекунд держит дескриптор файла. Поэтому перед следующим этапом можно сделать небольшое ожидание:
						await Task.Delay(300);

					}


					// ============================================
					// Ищем поврежденные таблицы
					// ============================================

					else if (
						step.Name ==
						"Ищем поврежденные таблицы")
					{
						
							var corrupted =
								_corruptedRecordsService
									.FindCorruptedRecords(
										appConfig.MdbPath);

							if (corrupted.Count > 0)
							{
								var sb = new StringBuilder();

								sb.AppendLine(
									"В базе данных обнаружены поврежденные записи.");
								sb.AppendLine();
								sb.AppendLine(
									"Проверьте следующие таблицы и поля:");
								sb.AppendLine();

								foreach (var item in corrupted)
								{
									sb.AppendLine(
										$"{item.TableName} -> {item.FieldName}");
								}

								throw new Exception(
									sb.ToString());
                        }
													
					}

					// ============================================
					// Проверка/Установка sql
					// ============================================

					else if (step.Name == "Установка sql")
					{
						//return;
						var sql =
							_sqlService
							.EnsureSqlInstalled();

						appConfig.SqlInstance =
							sql.InstanceName;

						// SQL по умолчанию !
						appConfig.SqlInstance =
							@".\SQLEXPRESS";

						// Формируем уникальное имя SQL базы
						// на основании WebServiceLogin
						appConfig.SqlDatabaseName =
							_sqlService.GetAvailableDatabaseName(
								appConfig.SqlInstance,
								appConfig.WebServiceLogin);

					}

					// ============================================
					// Установка bullzip msa2sql
					// ============================================

					else if (
						step.Name ==
						"Установка bullzip msa2sql")
					{
						string exePath =
							_bullzipService
								.EnsureInstalled();

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
					// Перезапуск службы sql
					// ============================================

					else if (
						step.Name ==
						"Перезапуск службы sql")
					{
						_sqlService
							.RestartSqlService(
								appConfig.SqlInstance);
					}


					// ============================================
					// Создаём rruser
					// ============================================

					else if (
						step.Name ==
						"Создаём rruser")
					{
						_sqlService
							.CreateRRUser(
								appConfig.SqlInstance);
					}

					// ============================================
					//  Патчим access базу
					// ============================================

					else if (


						step.Name ==
						"Патчим access базу")
					{
						//if (!isNewDatabase)
						//{
							_accessPatchService
							.ExecutePatches(
								appConfig.MdbPath);
						//}
					}

                    
                    // ============================================
                    // Конвертируем базу
                    // ============================================

                    else if (
                        step.Name ==
                        "Конвертируем базу")
                    {
                        // Конвертируем существующую Access-базу.
                            ConvertAccessDatabase(
                                owner,
                                appConfig);
                        
                    }

                    // ============================================
                    // Выдаём права rruser
                    // ============================================

                    else if (
						step.Name ==
						"Выдаём права rruser")
					{
						_sqlService
							.GrantDatabaseRights(
								appConfig.SqlInstance,
								appConfig.SqlDatabaseName);
					}
					// ============================================
					// Разрешаем TCP 1433 SQL. Назначаем порт 1433 для sql
					// ============================================

					else if (
						step.Name ==
						"Назначаем порт 1433 для sql")
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
					// Меняем connectionstrings
					// ============================================

					else if (
						step.Name ==
						"Меняем connectionstrings")
					{
						_configService.SwitchToSql(
							configPath,
							appConfig);
					}
					// ============================================
					// Патчим sql базу
					// ============================================

					else if (step.Name == "Патчим sql базу")
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
					// Установка sql managment
					// ============================================

					else if (
						step.Name ==
						"Установка sql management")
					{
						_ssmsService.EnsureInstalled();
					}


					// ============================================
					// Удаляем bullzip
					// ============================================


					else if (
						step.Name ==
						"Удаляем bullzip")
					{
						_bullzipService.Uninstall();
					}

					else
					{
						//await ExecuteStep(step);
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
				case "Установка sql":                    
                case "Сжимаем базу":
                case "Ищем поврежденные таблицы":
                case "Перезапуск службы sql":
				case "Создаём rruser":
				case "Конвертируем базу":
				
					return true;

				default:
					return false;
			}
		}
		/// <summary>
		/// Временная заглушка для
		/// ещё не реализованных шагов.
		/// </summary>
		//private async Task ExecuteStep(
		//	MigrationStep step)
		//{
		//	await Task.Delay(1000);
			
		//}

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
        new MigrationStep("Сжимаем базу"),
        new MigrationStep("Ищем поврежденные таблицы"),
        new MigrationStep("Установка sql"),
		new MigrationStep("Установка bullzip msa2sql"),
		new MigrationStep("Разрешаем mixed authentication"),
		new MigrationStep("Перезапуск службы sql"),
		new MigrationStep("Создаём rruser"),
		new MigrationStep("Патчим access базу"),
		new MigrationStep("Конвертируем базу"),
		new MigrationStep("Выдаём права rruser"),
		new MigrationStep("Назначаем порт 1433 для sql"),
		new MigrationStep("Открываем 1433 в брандмауэре"),
		new MigrationStep("Меняем connectionstrings"),
		new MigrationStep("Патчим sql базу"),
		new MigrationStep("Строка подключения в буфер обмена"),
		new MigrationStep("Запускаем УР"),
		};

            if (options.InstallSsms)
            {
                steps.Add(
                    new MigrationStep(
                        "Установка sql management"));
            }

            if (options.DeleteBullzip)
            {
                steps.Add(
                    new MigrationStep(
                        "Удаляем bullzip"));
            }

            return steps;
        }

        //Заменяет новую базу Access на пустую, но с нужной структурой. 
        /// <summary>
        /// Заменяет исходную базу данных
        /// пустой базой из каталога Misc.
        /// </summary>
        /// <param name="appConfig">
        private void ReplaceWithBlankAccessDatabase(
			AppConfig appConfig)
        {
            string blankDatabase =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    @"Misc\Data.mdb");

            if (!File.Exists(blankDatabase))
            {
                throw new FileNotFoundException(
                    "Не найден шаблон пустой базы Access.",
                    blankDatabase);
            }

            File.Copy(
                blankDatabase,
                appConfig.MdbPath,
                true);
        }
        /// <summary>
        /// Запускает RemoteReg.Launcher.exe
        /// </summary>
        private void StartRemoteReg(
			AppConfig appConfig)
		{
            string launcherPath = appConfig.ExePath.Replace("RemoteReg.exe", "RemoteReg.Launcher.exe");

            if (!File.Exists(
                launcherPath))
			{
				throw new Exception(
                    "RemoteReg.Launcher.exe не найден");
			}

            //Process.Start(
            //	appConfig.ExePath);

            var process =
			 new Process();

            process.StartInfo.FileName =
                launcherPath;

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

        /// <summary>
        /// Выполняет конвертацию Access-базы
        /// в SQL Server с помощью Bullzip.
        /// </summary>
        private void ConvertAccessDatabase(
            Form owner,
            AppConfig appConfig)
        {
            Process process =
                _converter.Convert(
                    appConfig);

            try
            {
                using (var logForm =
                    new ConversionLogForm(
                        owner,
                        _converter.GetLogFilePath()))
                {
                    DialogResult result =
                        logForm.ShowDialog(owner);

                    if (result == DialogResult.Abort)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit();
                        }

                        if (logForm.ConversionCanceledByUser)
                        {
                            throw new Exception(
                                "Конвертация была прервана пользователем.");
                        }

                        throw new Exception(
                            "Конвертация Bullzip завершилась с ошибкой.");
                    }
                }

                if (!process.HasExited)
                {
                    process.WaitForExit();
                }

                if (process.ExitCode != 0)
                {
                    throw new Exception(
                        "Ошибка конвертации Bullzip.\r\n\r\n" +
                        "Код завершения: " +
                        process.ExitCode);
                }
            }
            finally
            {
                try
                {
                    _bullzipService.CopyBullzipLog();
                }
                catch
                {
                    // Ошибка копирования лога
                    // не должна прерывать миграцию.
                }
            }
        }

    }
}