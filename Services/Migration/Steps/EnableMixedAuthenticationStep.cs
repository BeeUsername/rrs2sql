using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Включает смешанный режим аутентификации
    /// SQL Server (Windows + SQL).
    /// </summary>
    public class EnableMixedAuthenticationStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Разрешаем mixed authentication";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _sqlService.EnableMixedAuthentication(
                context.Config.SqlInstance);

            return Task.CompletedTask;
        }
    }
}