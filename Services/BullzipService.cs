using System;
using System.Diagnostics;
using System.IO;

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
	}
}