using Microsoft.Win32;
using RRStoSQL.Models;

namespace RRStoSQL.Services
{
	/// <summary>
	/// Работа с SQL Server
	/// </summary>
	public class SqlService
	{
		/// <summary>
		/// Ищет установленный SQLEXPRESS
		/// </summary>
		public SqlInstanceInfo FindSqlExpress()
		{
			const string registryPath =
				@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";

			using (RegistryKey key =
				Registry.LocalMachine.OpenSubKey(
					registryPath))
			{
				if (key == null)
				{
					return new SqlInstanceInfo
					{
						Exists = false
					};
				}

				var names = key.GetValueNames();

				foreach (string name in names)
				{
					if (name.ToUpper() == "SQLEXPRESS")
					{
						return new SqlInstanceInfo
						{
							Exists = true,
							InstanceName = @".\SQLEXPRESS"
						};
					}
				}
			}

			return new SqlInstanceInfo
			{
				Exists = false
			};
		}
	}
}