using System.Windows.Forms;

namespace RRStoSQL.Forms.UI
{
	public class StepListInitializer
	{
		public void Initialize(ListView listView)
		{
			listView.Items.Clear();

			AddStep(listView, "Читаем конфиг");
			AddStep(listView, "Установка SQL");
			AddStep(listView, "Разрешаем mixed authentication");
			AddStep(listView, "Создаём RRUser");
			AddStep(listView, "Перезапуск службы SQL");
			AddStep(listView, "Конвертируем базу");
			AddStep(listView, "Разрешаем TCP 1433");
			AddStep(listView, "Открываем порт 1433 в firewall");
			AddStep(listView, "Меняем connectionStrings");
			AddStep(listView, "Выполняем патчи");
			AddStep(listView, "Копируем строку подключения");
			AddStep(listView, "Запускаем УР");
		}

		private void AddStep(
			ListView listView,
			string stepName)
		{
			var item = new ListViewItem("⌛");

			item.SubItems.Add(stepName);

			listView.Items.Add(item);
		}
	}
}