using RRStoSQL.Helpers;
using RRStoSQL.Services;
using RRStoSQL.Utils;
using System;
using System.Text;
using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Проверяет наличие поврежденных записей
    /// в базе Access.
    /// </summary>
    public class CheckCorruptedRecordsStep : IMigrationStep
    {
        private readonly AccessCorruptedRecordsService
            _corruptedRecordsService =
                new AccessCorruptedRecordsService();

        public string Name =>
            "Ищем поврежденные таблицы";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            var corrupted =
                _corruptedRecordsService
                    .FindCorruptedRecords(
                        context.Config.MdbPath);

            if (corrupted.Count > 0)
            {
                var sb =
                    new StringBuilder();

                sb.AppendLine(
                    "В базе данных обнаружены поврежденные записи.");

                sb.AppendLine();

                sb.AppendLine(
                    "Проверьте следующие таблицы и поля:");

                sb.AppendLine();

                foreach (var item in corrupted)
                {
                    sb.AppendLine(
                        $"{item.TableName} -> {item.FieldName}");
                }

                MessageHelper.Show(
                    sb.ToString(),
                    "Обнаружены поврежденные записи");

                throw new Exception(
                    "Обнаружены поврежденные записи.");
            }

            return Task.CompletedTask;
        }
    }
}