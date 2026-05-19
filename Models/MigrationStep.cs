namespace RRStoSQL.Models
{
	public class MigrationStep
	{
		public string Name { get; set; }

		public MigrationStep(string name)
		{
			Name = name;
		}
	}
}