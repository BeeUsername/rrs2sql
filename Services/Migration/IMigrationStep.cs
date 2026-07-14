using RRStoSQL.Models;
using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration
{
    public interface IMigrationStep
    {
        string Name { get; }

        bool Critical { get; }

       // bool ShouldRun(MigrationOptions options);

        Task ExecuteAsync(
            MigrationContext context);
    }
}