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
                new CompactAccessStep());

            pipeline.Steps.Add(
                new CheckCorruptedRecordsStep());

            pipeline.Steps.Add(
                new EnsureSqlInstalledStep());

            pipeline.Steps.Add(
                new EnsureBullzipInstalledStep());

            pipeline.Steps.Add(
                new EnableMixedAuthenticationStep());

            pipeline.Steps.Add(
                new RestartSqlServiceStep());
                             

            
            pipeline.Steps.Add(
                new CreateRRUserStep());

            pipeline.Steps.Add(
                 new PatchAccessDatabaseStep());

            pipeline.Steps.Add(
             new ConvertAccessDatabaseStep());
            
            pipeline.Steps.Add(
                new GrantDatabaseRightsStep());

            pipeline.Steps.Add(
                new EnableSqlTcpStep());

            pipeline.Steps.Add(
                   new OpenFirewallPortStep());

            pipeline.Steps.Add(
                 new SwitchConnectionStringsStep());

            pipeline.Steps.Add(
                  new PatchSqlDatabaseStep());

            pipeline.Steps.Add(
                 new CopyConnectionStringStep());

            pipeline.Steps.Add(
                 new StartRemoteRegStep());
                        

            pipeline.Steps.Add(
                 new EnsureSsmsInstalledStep());

            pipeline.Steps.Add(
                 new UninstallBullzipStep());

            return pipeline;
        }


    }

}