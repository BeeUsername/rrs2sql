using System.Windows.Forms;
using RRStoSQL.Models;

namespace RRStoSQL.Forms.UI
{
	public class StepListInitializer
	{
		public void Initialize(ListView listView)
		{
			listView.Items.Clear();

			AddStep(listView, "Читаем конфиг");
			AddStep(listView, "Установка SQL");
			AddStep(listView, "Установка Bullzip msa2sql");			
			AddStep(listView, "Разрешаем mixed authentication");
			AddStep(listView, "Перезапуск службы SQL");
			AddStep(listView, "Создаём RRUser");
			AddStep(listView, "Патчим Access базу");			
			AddStep(listView, "Конвертируем базу");
			AddStep(listView, "Выдаём права RRUser");
			AddStep(listView, "Назначаем порт 1433 для SQL");
			AddStep(listView, "Открываем 1433 в брандмауэре");
			AddStep(listView, "Меняем connectionStrings");
			AddStep(listView, "Патчим SQL базу");
			AddStep(listView, "Строка подключения в буфер обмена");
			AddStep(listView, "Запускаем УР");
            AddStep(listView, "Установка SQL Management");
            AddStep(listView, "Удаляем Bullzip");

        }

		private void AddStep(
			ListView listView,
			string stepName)
		{
			var item = new ListViewItem("...");

			item.SubItems.Add(stepName);

			listView.Items.Add(item);
		}
	}
}