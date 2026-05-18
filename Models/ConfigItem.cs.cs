namespace MigrateFromAccess.Models
{
	public class ConfigItem
	{
		public string ConfigPath { get; set; }

		public override string ToString()
		{
			return ConfigPath;
		}
	}
}