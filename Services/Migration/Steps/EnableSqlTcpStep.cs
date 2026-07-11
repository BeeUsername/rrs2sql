using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Включает TCP/IP для SQL Server
    /// и перезапускает службу,
    /// чтобы изменения вступили в силу.
    /// </summary>
    public class EnableSqlTcpStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Назначаем порт 1433 для sql";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            // Включаем TCP/IP
            _sqlService.EnableTcp(
                context.Config.SqlInstance);

            // Настройки сети применяются
            // только после перезапуска службы.
            _sqlService.RestartSqlService(
                context.Config.SqlInstance);

            return Task.CompletedTask;
        }
    }
}