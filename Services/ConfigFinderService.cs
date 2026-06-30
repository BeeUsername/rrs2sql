using MigrateFromAccess.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MigrateFromAccess.Services
{
    /// <summary>
    /// Поиск файлов RemoteReg.exe.config
    /// в стандартных каталогах установки УР.
    /// </summary>
    public class ConfigFinderService
    {
        /// <summary>
        /// Каталоги, которые не нужно сканировать
        /// при рекурсивном обходе C:\Citilab.
        /// </summary>
        private static readonly string[] ExcludedFolders =
        {
            "Log",
            "Data",
            "Backup",
            "Backups",
            "Utility"
        };

        /// <summary>
        /// Возвращает список найденных конфигов.
        /// Поиск выполняется:
        /// - в C:\Citilab
        /// - в Program Files
        /// - в Program Files (x86)
        /// </summary>
        public List<ConfigItem> FindConfigs()
        {
            var result = new List<ConfigItem>();

            // Поиск в C:\Citilab
            SearchCitilab(result);

            // Поиск в Program Files
            SearchProgramFiles(result);

            // Удаляем дубликаты,
            // сортируем по пути
            return result
                .GroupBy(x => x.ConfigPath.ToLower())
                .Select(x => x.First())
                .OrderBy(x => x.ConfigPath)
                .ToList();
        }

        /// <summary>
        /// Запускает рекурсивный поиск
        /// внутри каталога C:\Citilab.
        /// </summary>
        private void SearchCitilab(
            List<ConfigItem> result)
        {
            string root = @"C:\Citilab";

            if (!Directory.Exists(root))
                return;

            SearchDirectory(
                root,
                result);
        }

        /// <summary>
        /// Рекурсивный обход каталогов.
        /// Ищет файл RemoteReg.exe.config
        /// в текущей папке и затем
        /// переходит во вложенные каталоги.
        /// </summary>
        private void SearchDirectory(
            string directory,
            List<ConfigItem> result)
        {
            try
            {
                var dirInfo =
                    new DirectoryInfo(
                        directory);

                // Пропускаем служебные каталоги
                if (ShouldSkipFolder(
                    dirInfo.Name))
                {
                    return;
                }

                // Ищем конфиг только
                // в текущей папке
                var files =
                    dirInfo.GetFiles(
                        "RemoteReg.exe.config",
                        SearchOption.TopDirectoryOnly);

                // Добавляем найденные конфиги
                foreach (var file in files)
                {
                    result.Add(
                        new ConfigItem
                        {
                            ConfigPath =
                                file.FullName
                        });
                }

                // Рекурсивно обходим
                // все вложенные каталоги
                foreach (var subDir in
                    dirInfo.GetDirectories())
                {
                    SearchDirectory(
                        subDir.FullName,
                        result);
                }
            }
            catch
            {
                // Игнорируем ошибки доступа
                // и продолжаем поиск
            }
        }

        /// <summary>
        /// Проверяет,
        /// нужно ли пропускать каталог.
        /// </summary>
        private bool ShouldSkipFolder(
            string folderName)
        {
            // Каталог находится
            // в списке исключений
            if (ExcludedFolders.Any(
                x => folderName.Equals(
                    x,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Пропускаем каталоги генератора
            if (folderName.IndexOf(
                    "Генератор",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ищет установленные версии УР
        /// в Program Files и Program Files (x86).
        /// </summary>
        private void SearchProgramFiles(
            List<ConfigItem> result)
        {
            var roots = new[]
            {
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),

                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var root in roots.Distinct())
            {
                if (!Directory.Exists(root))
                    continue;

                try
                {
                    // Проверяем только каталоги
                    // первого уровня
                    foreach (var dir in
                        Directory.GetDirectories(root))
                    {
                        string dirName =
                            Path.GetFileName(dir);

                        // Нас интересуют только
                        // каталоги УР
                        if (!dirName.Contains(
                                "Remote Registration Software"))
                        {
                            continue;
                        }

                        // Предполагаемый путь
                        // к конфигу
                        string configPath =
                            Path.Combine(
                                dir,
                                "RemoteReg.exe.config");

                        // Если конфиг найден —
                        // добавляем его в список
                        if (File.Exists(configPath))
                        {
                            result.Add(
                                new ConfigItem
                                {
                                    ConfigPath =
                                        configPath
                                });
                        }
                    }
                }
                catch
                {
                    // Ошибки доступа
                    // не должны прерывать поиск
                }
            }
        }
    }
}


//using MigrateFromAccess.Models;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;

//namespace MigrateFromAccess.Services
//{
//	public class ConfigFinderService
//	{
//		private static readonly string[] ExcludedFolders =
//		{
//			"Log",
//			"Data",
//			"Backup",
//			"Backups",
//			"Utility"
//		};

//		public List<ConfigItem> FindConfigs()
//		{
//			var result = new List<ConfigItem>();

//			SearchCitilab(result);

//			SearchProgramFiles(result);

//			return result
//				.GroupBy(x => x.ConfigPath.ToLower())
//				.Select(x => x.First())
//				.OrderBy(x => x.ConfigPath)
//				.ToList();
//		}

//		private void SearchCitilab(List<ConfigItem> result)
//		{
//			string root = @"C:\Citilab";

//			if (!Directory.Exists(root))
//				return;

//			SearchDirectory(root, result);
//		}

//		private void SearchDirectory(
//			string directory,
//			List<ConfigItem> result)
//		{
//			try
//			{
//				var dirInfo = new DirectoryInfo(directory);

//				if (ShouldSkipFolder(dirInfo.Name))
//					return;

//				var files = dirInfo.GetFiles(
//					"RemoteReg.exe.config",
//					SearchOption.TopDirectoryOnly);

//				foreach (var file in files)
//				{
//					result.Add(new ConfigItem
//					{
//						ConfigPath = file.FullName
//					});
//				}

//				foreach (var subDir in dirInfo.GetDirectories())
//				{
//					SearchDirectory(subDir.FullName, result);
//				}
//			}
//			catch
//			{
//			}
//		}

//		private bool ShouldSkipFolder(string folderName)
//		{
//			if (ExcludedFolders.Any(x =>
//				folderName.Equals(
//					x,
//					StringComparison.OrdinalIgnoreCase)))
//			{
//				return true;
//			}

//			if (folderName.IndexOf(
//				"Генератор",
//				StringComparison.OrdinalIgnoreCase) >= 0)
//			{
//				return true;
//			}

//			return false;
//		}

//		private void SearchProgramFiles(List<ConfigItem> result)
//		{
//			var roots = new[]
//			{
//				Environment.GetFolderPath(
//					Environment.SpecialFolder.ProgramFiles),

//				Environment.GetFolderPath(
//					Environment.SpecialFolder.ProgramFilesX86)
//			};

//			foreach (var root in roots.Distinct())
//			{
//				if (!Directory.Exists(root))
//					continue;

//				try
//				{
//					foreach (var dir in Directory.GetDirectories(root))
//					{
//						string dirName =
//							Path.GetFileName(dir);

//						if (!dirName.Contains(
//							"Remote Registration Software"))
//						{
//							continue;
//						}

//						string configPath = Path.Combine(
//							dir,
//							"RemoteReg.exe.config");

//						if (File.Exists(configPath))
//						{
//							result.Add(new ConfigItem
//							{
//								ConfigPath = configPath
//							});
//						}
//					}
//				}
//				catch
//				{
//				}
//			}
//		}
//	}
//}