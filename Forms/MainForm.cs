using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RRStoSQL
{
	public partial class MainForm : Form
	{
		public MainForm()
		{
			InitializeComponent();
			InitializeSteps();
		}

		private void InitializeSteps()
		{
			AddStep("Читаем конфиг");
			AddStep("Установка SQL");
			AddStep("Разрешаем mixed authentication");
			AddStep("Создаём RRUser");
			AddStep("Перезапуск службы SQL");
			AddStep("Конвертируем базу");
			AddStep("Разрешаем TCP 1433");
			AddStep("Открываем порт 1433 в firewall");
			AddStep("Меняем connectionStrings");
			AddStep("Выполняем патчи");
			AddStep("Копируем строку подключения");
			AddStep("Запускаем УР");
		}

		private void AddStep(string stepName)
		{
			var item = new ListViewItem("⌛");

			item.SubItems.Add(stepName);

			listViewSteps.Items.Add(item);
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
	}
}
