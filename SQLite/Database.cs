using Microsoft.Data.Sqlite;

namespace SQLite {
	public static class Database {
		public static void Connect() {
			using (var connection = new SqliteConnection("Data Source=hello.db")) {
				connection.Open();


			}
		}
	}
}