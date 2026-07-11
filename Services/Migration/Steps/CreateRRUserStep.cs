using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Создает SQL-пользователя RRUser.
    /// </summary>
    public class CreateRRUserStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Создаём rruser";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _sqlService.CreateRRUser(
                context.Config.SqlInstance);

            return Task.CompletedTask;
        }
    }
}