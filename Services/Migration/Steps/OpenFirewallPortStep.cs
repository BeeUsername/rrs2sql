using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Открывает TCP-порт 1433
    /// в брандмауэре Windows.
    /// </summary>
    public class OpenFirewallPortStep
        : IMigrationStep
    {
        private readonly SqlService
            _sqlService =
                new SqlService();

        public string Name =>
            "Открываем 1433 в брандмауэре";

        public bool Critical =>
            false;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _sqlService.OpenFirewallPort();

            return Task.CompletedTask;
        }
    }
}