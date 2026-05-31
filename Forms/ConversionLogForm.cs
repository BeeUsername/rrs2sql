using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace RRStoSQL.Forms
{
	public partial class ConversionLogForm : Form
	{
		private readonly string _logFile;

		private readonly Timer _timer =
			new Timer();

		public ConversionLogForm(
	string logFile,
	Process process)
		{
			InitializeComponent();

			_logFile =
				logFile;

			_timer.Interval =
				1000;

			_timer.Tick +=
				Timer_Tick;

			_timer.Start();   
		}

		private void ConversionLogForm_Load(
			object sender,
			EventArgs e)
		{
			_timer.Start();
		}

		private void Timer_Tick(
			object sender,
			EventArgs e)
		{
			try
			{
				if (!File.Exists(
					_logFile))
				{
					return;
				}

				string text =
					File.ReadAllText(
						_logFile);

				txtLog.Text =
					text;

				txtLog.SelectionStart =
					txtLog.TextLength;

				txtLog.ScrollToCaret();

				if (text.IndexOf(
		"Transfer completed",
		StringComparison.OrdinalIgnoreCase) >= 0)
				{
					_timer.Stop();

					DialogResult =
						DialogResult.OK;

					Close();
				}

				if (text.IndexOf(
		"error",
		StringComparison.OrdinalIgnoreCase) >= 0)
				{
					_timer.Stop();

					MessageBox.Show(
						"Во время конвертации обнаружена ошибка.\n\n" +
						"Проверьте лог.");

					DialogResult =
						DialogResult.Abort;

					Close();
				}
			}
			catch
			{
				// лог ещё пишется
			}
		}
	}
}