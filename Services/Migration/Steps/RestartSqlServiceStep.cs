using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Перезапускает службу SQL Server,
    /// чтобы применить изменения конфигурации.
    /// </summary>
    public class RestartSqlServiceStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Перезапуск службы sql";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _sqlService.RestartSqlService(
                context.Config.SqlInstance);

            return Task.CompletedTask;
        }
    }
}