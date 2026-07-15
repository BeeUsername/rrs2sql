
using RRStoSQL.Models;
using System.Windows.Forms;

namespace RRStoSQL.Forms.UI
{
    /// <summary>
    /// Отвечает за отображение состояния
    /// шагов миграции в ListView.
    /// </summary>
    public class StepStatusManager
    {
        /// <summary>
        /// Ссылка на ListView со списком шагов.
        /// </summary>
        private readonly ListView _listView;

        /// <summary>
        /// Создает менеджер отображения
        /// статусов шагов.
        /// </summary>
        /// <param name="listView">
        /// ListView со списком этапов миграции.
        /// </param>
        public StepStatusManager(
            ListView listView)
        {
            _listView = listView;
        }

        /// <summary>
        /// Устанавливает статус шага
        /// по его имени.
        /// </summary>
        /// <param name="stepName">
        /// Название шага миграции.
        /// </param>
        /// <param name="status">
        /// Новый статус шага.
        /// </param>
        public void SetStatus(
            string stepName,
            StepStatus status)
        {
            // Ищем нужный шаг
            // по названию во второй колонке
            for (int i = 0;
                 i < _listView.Items.Count;
                 i++)
            {
                var item = _listView.Items[i];

                if (item.SubItems[1].Text == stepName)
                {
                    SetStatus(
                        i,
                        status);

                    return;
                }
            }
        }

        /// <summary>
        /// Устанавливает статус шага
        /// по его индексу в списке.
        /// </summary>
        /// <param name="index">
        /// Индекс строки ListView.
        /// </param>
        /// <param name="status">
        /// Новый статус шага.
        /// </param>
        private void SetStatus(
            int index,
            StepStatus status)
        {
            // Проверяем корректность индекса
            if (index < 0 ||
                index >= _listView.Items.Count)
            {
                return;
            }

            // Иконка ожидания по умолчанию
            string icon = "⌛";

            switch (status)
            {
                // Шаг еще не выполнялся
                case StepStatus.Waiting:
                    //icon = "⌛";
                    icon = "...";
                    break;

                // Шаг выполняется в данный момент
                case StepStatus.Running:
                    icon = "⏳";
                    break;

                // Шаг успешно завершен
                case StepStatus.Success:
                    icon = "✓";
                    break;

                // Во время выполнения возникла ошибка
                case StepStatus.Error:
                    icon = "❌";
                    break;
            }

            // Устанавливаем иконку статуса
            // в первую колонку ListView
            _listView.Items[index].Text = icon;

            // Автоматически прокручиваем список,
            // чтобы текущий шаг был виден пользователю
            _listView.Items[index]
                .EnsureVisible();
        }
    }
}

