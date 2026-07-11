using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Выдает пользователю RRUser права
    /// на созданную базу данных.
    /// </summary>
    public class GrantDatabaseRightsStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Выдаём права rruser";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _sqlService.GrantDatabaseRights(
                context.Config.SqlInstance,
                context.Config.SqlDatabaseName);

            return Task.CompletedTask;
        }
    }
}