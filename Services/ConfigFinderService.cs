using MigrateFromAccess.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MigrateFromAccess.Services
{
	public class ConfigFinderService
	{
		private static readonly string[] ExcludedFolders =
		{
			"Log",
			"Data",
			"Backup",
			"Backups",
			"Utility"
		};

		public List<ConfigItem> FindConfigs()
		{
			var result = new List<ConfigItem>();

			SearchCitilab(result);

			SearchProgramFiles(result);

			return result
				.GroupBy(x => x.ConfigPath.ToLower())
				.Select(x => x.First())
				.OrderBy(x => x.ConfigPath)
				.ToList();
		}

		private void SearchCitilab(List<ConfigItem> result)
		{
			string root = @"C:\Citilab";

			if (!Directory.Exists(root))
				return;

			SearchDirectory(root, result);
		}

		private void SearchDirectory(
			string directory,
			List<ConfigItem> result)
		{
			try
			{
				var dirInfo = new DirectoryInfo(directory);

				if (ShouldSkipFolder(dirInfo.Name))
					return;

				var files = dirInfo.GetFiles(
					"RemoteReg.exe.config",
					SearchOption.TopDirectoryOnly);

				foreach (var file in files)
				{
					result.Add(new ConfigItem
					{
						ConfigPath = file.FullName
					});
				}

				foreach (var subDir in dirInfo.GetDirectories())
				{
					SearchDirectory(subDir.FullName, result);
				}
			}
			catch
			{
			}
		}

		private bool ShouldSkipFolder(string folderName)
		{
			if (ExcludedFolders.Any(x =>
				folderName.Equals(
					x,
					StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}

			if (folderName.IndexOf(
				"Генератор",
				StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			return false;
		}

		private void SearchProgramFiles(List<ConfigItem> result)
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
					foreach (var dir in Directory.GetDirectories(root))
					{
						string dirName =
							Path.GetFileName(dir);

						if (!dirName.Contains(
							"Remote Registration Software"))
						{
							continue;
						}

						string configPath = Path.Combine(
							dir,
							"RemoteReg.exe.config");

						if (File.Exists(configPath))
						{
							result.Add(new ConfigItem
							{
								ConfigPath = configPath
							});
						}
					}
				}
				catch
				{
				}
			}
		}
	}
}