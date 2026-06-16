using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using static System.Resources.ResXFileRef;

namespace RRStoSQL.Services
{
	public class BullzipService
	{
		private const string InstallFolder =
			@"C:\Program Files (x86)\Bullzip\MS Access to MSSQL";

		private const string ExeName =
			"msa2sql.exe";

		public string EnsureInstalled()
		{
			string exePath =
				Path.Combine(
					InstallFolder,
					ExeName);

			// Уже установлен
			if (File.Exists(exePath))
			{
				return exePath;
			}

			// Установщик рядом с RRStoSQL.exe
			string installerPath =
				Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"Bullzip",
					"msa2sql_5_5_0_281.exe");

	//		MessageBox.Show(
	//$"BaseDirectory:\n{AppDomain.CurrentDomain.BaseDirectory}\n\n" +
	//$"Installer:\n{installerPath}\n\n" +
	//$"Exists: {File.Exists(installerPath)}");

			if (!File.Exists(installerPath))
			{
				throw new Exception(
					"Не найден установщик Bullzip:\n\n" +
					installerPath);
			}

			Process process =
				new Process();

			process.StartInfo.FileName =
				installerPath;

			process.StartInfo.Arguments =
				"/VERYSILENT " +
				"/SUPPRESSMSGBOXES " +
				"/NORESTART";

			process.StartInfo.UseShellExecute =
				true;

			process.StartInfo.Verb =
				"runas";

			process.Start();

			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception(
					"Ошибка установки Bullzip.\n\n" +
					"ExitCode=" +
					process.ExitCode);
			}

			if (!File.Exists(exePath))
			{
				throw new Exception(
					"Bullzip установлен, но msa2sql.exe не найден.");
			}

			return exePath;
		}

		public void CopyBullzipLog()
		{
			string sourceLog =
				Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.ApplicationData),
					@"Bullzip\MS Access to MSSQL\msa2sql.log");

			if (!File.Exists(sourceLog))
			{
				return;
			}

			string logsFolder =
				Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"logs");

			Directory.CreateDirectory(
				logsFolder);

			string destLog =
				Path.Combine(
					logsFolder,
					$"msa2sql_{DateTime.Now:yyyyMMdd_HHmmss}.log");

			File.Copy(
				sourceLog,
				destLog,
				true);
		}
	}
}