using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RRStoSQL.Models;

namespace RRStoSQL.Services
{
	public class AccessConverterService
	{
		/// <summary>
		/// Выполняет конвертацию MDB → SQL
		/// через Bullzip msa2sql.exe
		/// </summary>
		public Process Convert(
			AppConfig appConfig)
		{
			UpdateSettingsFile(
				appConfig);

			return RunBullzip();
		}

		/// <summary>
		/// Обновляет settings.ini
		/// </summary>
		private void UpdateSettingsFile(
			AppConfig appConfig)
		{
			string settingsFile =
				Path.Combine(
					AppDomain.CurrentDomain
						.BaseDirectory,
					"Bullzip",
					"settings.ini");

			if (!File.Exists(
				settingsFile))
			{
				throw new Exception(
					"Не найден файл:\n\n" +
					settingsFile);
			}

			string text =
				File.ReadAllText(
					settingsFile);

			// MDB
			text =
				ReplaceIniValue(
					text,
					"sourcefilename",
					appConfig.MdbPath);

			// SQL Server
			text =
				ReplaceIniValue(
					text,
					"destinationserver",
					appConfig.SqlInstance);

			// База данных
			text =
				ReplaceIniValue(
					text,
					"destinationdatabase",
					appConfig.SqlDatabaseName);

			File.WriteAllText(
				settingsFile,
				text);
		}

		/// <summary>
		/// Заменяет значение параметра
		/// в settings.ini
		/// </summary>
		private string ReplaceIniValue(
	string text,
	string key,
	string value)
		{
			string[] lines =
				text.Split(
					new[]
					{
				"\r\n",
				"\n"
					},
					StringSplitOptions.None);

			for (int i = 0;
				 i < lines.Length;
				 i++)
			{
				string line =
					lines[i].TrimStart();

				if (line.StartsWith(
						key + "=",
						StringComparison.OrdinalIgnoreCase))
				{
					// сохраняем исходный отступ
					int indentLength =
						lines[i].Length -
						lines[i].TrimStart().Length;

					string indent =
						new string(
							' ',
							indentLength);

					lines[i] =
						indent +
						key +
						"=" +
						value;
				}
			}

			return string.Join(
				Environment.NewLine,
				lines);
		}

		/// <summary>
		/// Запускает Bullzip
		/// </summary>
		private Process RunBullzip()
		{
			//string exeFile =
			//	Path.Combine(
			//		AppDomain.CurrentDomain.BaseDirectory,
			//		"Bullzip",
			//		//"msa2sql.exe");
			//		"Bullzip.exe");

			string exeFile =
				Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.ProgramFilesX86),
					@"Bullzip\MS Access to MSSQL\msa2sql.exe");

			string settingsFile =
				Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"Bullzip",
					"settings.ini");

			Process process =
				new Process();

			process.StartInfo.FileName =
				exeFile;

			process.StartInfo.Arguments =
				$"SETTINGS=\"{settingsFile}\" " +
				"AUTORUN " +
				"HIDE " +
				"CLEARLOG";

			process.StartInfo.UseShellExecute =
				false;

			process.StartInfo.CreateNoWindow =
				true;

			process.Start();

			return process;
		}

		/// <summary>
		/// Возвращает путь
		/// к файлу лога Bullzip
		/// </summary>
		public string GetLogFilePath()
		{
			return Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.ApplicationData),
					@"Bullzip\MS Access to MSSQL\msa2sql.log");
			//return Path.Combine(
			//	AppDomain.CurrentDomain.BaseDirectory,
			//	"Bullzip",
			//	"Bullzip MS Access to MSSQL.cameyo.files",
			//	"CHANGES",
			//	"%AppData%",
			//	"Bullzip",
			//	"MS Access to MSSQL",
			//	"msa2sql.log");


		}
	}
}