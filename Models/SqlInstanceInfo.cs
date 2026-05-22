namespace RRStoSQL.Models
{
	/// <summary>
	/// Информация о найденном SQL instance
	/// </summary>
	public class SqlInstanceInfo
	{
		/// <summary>
		/// Имя instance
		/// Например:
		/// .\SQLEXPRESS
		/// </summary>
		public string InstanceName { get; set; }

		/// <summary>
		/// SQL найден
		/// </summary>
		public bool Exists { get; set; }
	}
}