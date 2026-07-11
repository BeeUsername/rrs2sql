using RRStoSQL.Services.Migration.Steps;

namespace RRStoSQL.Services.Migration
{
    /// <summary>
    /// Формирует последовательность шагов миграции.
    /// </summary>
    public static class MigrationPipelineFactory
    {
        public static MigrationPipeline Create()
        {
            var pipeline =
                new MigrationPipeline();

            pipeline.Steps.Add(
                new ReadConfigStep());

            pipeline.Steps.Add(
                new CheckCorruptedRecordsStep());

            pipeline.Steps.Add(
                new CompactAccessStep());

            return pipeline;
        }


    }

}