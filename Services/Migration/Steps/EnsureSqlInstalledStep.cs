using System.Threading.Tasks;
using RRStoSQL.Services;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Проверяет наличие SQL Server,
    /// при необходимости устанавливает его
    /// и формирует параметры подключения.
    /// </summary>
    public class EnsureSqlInstalledStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Установка sql";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            var sql =
                _sqlService
                    .EnsureSqlInstalled();

            //context.Config.SqlInstance =
            //    sql.InstanceName;

            // Пока оставляем как было
            context.Config.SqlInstance =
                @".\SQLEXPRESS";

            context.Config.SqlDatabaseName =
                _sqlService.GetAvailableDatabaseName(
                    context.Config.SqlInstance,
                    context.Config.WebServiceLogin);

            return Task.CompletedTask;
        }
    }
}