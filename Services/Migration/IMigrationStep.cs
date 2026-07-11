using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration
{
    public interface IMigrationStep
    {
        string Name { get; }

        bool Critical { get; }

        Task ExecuteAsync(
            MigrationContext context);
    }
}