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
            AddStep(listView, "Ищем поврежденные таблицы");
            AddStep(listView, "Компактим базу");
            AddStep(listView, "Патчим access базу");

            AddStep(listView, "Установка sql");
            AddStep(listView, "Установка bullzip msa2sql");			
			AddStep(listView, "Разрешаем mixed authentication");
			AddStep(listView, "Перезапуск службы sql");
			AddStep(listView, "Создаём rruser");
						
			AddStep(listView, "Конвертируем базу");
			AddStep(listView, "Выдаём права rruser");
			AddStep(listView, "Назначаем порт 1433 для sql");
			AddStep(listView, "Открываем 1433 в брандмауэре");
			
			AddStep(listView, "Патчим sql базу");
            AddStep(listView, "Правим конфиг");
            AddStep(listView, "Строка подключения в буфер обмена");
			AddStep(listView, "Запускаем УР");
            AddStep(listView, "Установка sql management");
            AddStep(listView, "Удаляем bullzip");

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