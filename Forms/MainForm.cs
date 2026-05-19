using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MigrateFromAccess.Models;
using MigrateFromAccess.Services;
using RRStoSQL.Forms.UI;
using RRStoSQL.Models;
using RRStoSQL.Services;

namespace RRStoSQL
{
	public partial class MainForm : Form
	{

		private readonly ConfigFinderService _finderService =
			new ConfigFinderService();

		private readonly StepListInitializer
	_stepInitializer =
		new StepListInitializer();

		private StepStatusManager _stepManager;

		private readonly MigrationService _migrationService =
			new MigrationService();

		public MainForm()
		{
			InitializeComponent();
			//InitializeSteps();
			//LoadConfigs();
			//SetStepStatus("Читаем коффиг", StepStatus.Success);
			//SetStepStatus("Читаем конфиг", StepStatus.Error);
			//SetStepStatus(2, StepStatus.Error);


			_stepInitializer.Initialize(listViewSteps);

			_stepManager =
				new StepStatusManager(listViewSteps);

			LoadConfigs();

			_stepManager.SetStatus(
				"Читаем конфиг",
				StepStatus.Success);

			_stepManager.SetStatus(
				"Установка SQL",
				StepStatus.Running);

			_stepManager.SetStatus(
				"Разрешаем mixed authentication",
				StepStatus.Error);
		}

	
		private void LoadConfigs()
		{
			listBoxConfigs.Items.Clear();

			var configs = _finderService.FindConfigs();

			foreach (var config in configs)
			{
				listBoxConfigs.Items.Add(config);
			}
		}

		








		private void panel1_Paint(object sender, PaintEventArgs e)
		{

		}

		private void MainForm_Load(object sender, EventArgs e)
		{

		}

		private void groupBox1_Enter(object sender, EventArgs e)
		{

		}

		private void listBoxConfigs_SelectedIndexChanged(object sender, EventArgs e)
		{

		}

		private void panel1_Paint_1(object sender, PaintEventArgs e)
		{

		}

		private void listViewSteps_SelectedIndexChanged(object sender, EventArgs e)
		{

		}

		private void btnAddConfig_Click(
	object sender,
	EventArgs e)
		{
			using (var dialog = new OpenFileDialog())
			{
				dialog.Filter =
					"RemoteReg config|RemoteReg.exe.config";

				dialog.Multiselect = false;

				if (dialog.ShowDialog() != DialogResult.OK)
					return;

				string selectedPath = dialog.FileName;

				foreach (ConfigItem item in listBoxConfigs.Items)
				{
					if (item.ConfigPath.Equals(
						selectedPath,
						StringComparison.OrdinalIgnoreCase))
					{
						listBoxConfigs.SelectedItem = item;

						MessageBox.Show(
							"Конфиг уже добавлен");

						return;
					}
				}

				var config = new ConfigItem
				{
					ConfigPath = selectedPath
				};

				listBoxConfigs.Items.Add(config);

				listBoxConfigs.SelectedItem = config;
			}
		}

		private void groupBox2_Enter(object sender, EventArgs e)
		{

		}

		private async void btnConvert_Click(
	object sender,
	EventArgs e)
		{
			if (listBoxConfigs.SelectedItem == null)
			{
				MessageBox.Show(
					"Выберите конфиг");

				return;
			}

			btnConvert.Enabled = false;

			try
			{
				await _migrationService.RunAsync(
					(step, status) =>
					{
						_stepManager.SetStatus(
							step,
							status);

						Application.DoEvents();
					});

				MessageBox.Show(
					"Конвертация завершена");
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					ex.Message);
			}
			finally
			{
				btnConvert.Enabled = true;
			}
		}
	}
}
