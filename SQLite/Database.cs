using Microsoft.Data.Sqlite;
using System.Data;

namespace SQLite {
	public static class Database {
		public static void Connect() {
			using (var connection = new SqliteConnection("Data Source=hello.db")) {
				connection.Open();

				using var cmd = new SqliteCommand("select * from [test]", connection);

				using var rdr = cmd.ExecuteReader();

				while (rdr.Read()) {
					var obj = new object[rdr.FieldCount];
					rdr.GetValues(obj);

					Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(obj));
				}

				//using var cmd = new SQLiteCommand(con);
				//cmd.CommandText = "INSERT INTO cars(name, price) VALUES(@name, @price)";

				//cmd.Parameters.AddWithValue("@name", "BMW");
				//cmd.Parameters.AddWithValue("@price", 36600);
				//cmd.Prepare();

				//cmd.ExecuteNonQuery();
			}
		}
	}
}