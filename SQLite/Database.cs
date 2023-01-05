using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace RusysDev.SQLite {
	using Config;

	public class Sql {
		private static string? DbConn { get; set; }
		public static string Database { get => DbConn ?? Init("main.db"); set { Init(value); } }

		public static SqlConfig Config { get; } = new();

		private static string Init(string path) {
			var pt = $"Data Source={path}";
			if (DbConn != pt) { DbConn = pt; new SqlUpdate().PrintUpdates(); }
			return DbConn;
		}

		public static List<string> GetTables() => new Sql("SELECT Name FROM sqlite_master WHERE type='table' and name not like 'sqlite_%'").GetList<string>();
		public static List<SqlColumn> GetFields(string table) => new Sql("SELECT * FROM PRAGMA_TABLE_INFO($tbl)", "$tbl", table).GetData<SqlColumn>();


		public string Query { get; set; }
		public SqlParams Params { get; }
		private SqliteTransaction? Tran { get; set; }
		private SqliteConnection Conn() { var conn = new SqliteConnection(Database); conn.Open(); return conn; }
		private SqliteCommand Cmd(SqliteConnection conn) {
			var cmd = new SqliteCommand(Query, conn, Tran);
			foreach (var i in Params) { cmd.Parameters.Add(new SqliteParameter(i.Item1, i.Item2 ?? DBNull.Value)); }
			Print();
			return cmd;
		}

		public Sql(string sql) { Query = sql; Params = new(); }
		public Sql(string sql, string param, object? value) { Query = sql; (Params = new()).Add(param, value); }
		public Sql(string sql, params ValueTuple<string, object?>[] param) { Query = sql; Params = new(); foreach (var i in param) { Params.Add(i); } }
		public Sql(string sql, SqlParams param) { Query = sql; Params = param; }


		public List<object[]> GetArray() {
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			var ret = new List<object[]>();
			var cnt = rdr.FieldCount;
			while (rdr.Read()) { var obj = new object[cnt]; rdr.GetValues(obj); ret.Add(obj); }
			return ret;
		}


		public List<T> GetData<T>() where T : new() {
			var ret = new List<T>();
			var pr = SqlProps.Get<T>();
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) ret.Add(pr.Fill<T>(rdr));
			return ret;
		}

		public int Execute() {
			using var conn = Conn();
			using var cmd = Cmd(conn);
			return cmd.ExecuteNonQuery();
		}

		public int Execute<T>(T obj) {
			var pr = SqlProps.Get<T>();
			foreach (var i in pr) { Params.Add((i.Key, i.GetValue(obj))); }
			return Execute();
		}

		public int Execute<T>(List<T> obj) {
			int ret = 0; var pr = SqlProps.Get<T>();
			foreach (var i in pr) { Params.Add((i.Key, null)); }
			using var conn = Conn();
			using var tran = Tran = conn.BeginTransaction();
			using var cmd = Cmd(conn);
			try {
				var prm = GetParams(cmd);
				foreach (var i in obj) {
					foreach (var j in pr) prm[j.Key].Value = j.GetValue(i);
					ret += cmd.ExecuteNonQuery();
				}
				tran.Commit();
			} catch (Exception) { tran.Rollback(); throw; }
			return ret;
		}

		public List<T> GetList<T>(int field = 0) {
			var ret = new List<T>();
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) ret.Add(rdr.GetFieldValue<T>(field));
			return ret;
		}

		public List<T> GetListJson<T>(int field = 0) {
			var ret = new List<T>();
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) {
				try {
					var obj = JsonSerializer.Deserialize<T>(rdr.GetString(field));
					if (obj is not null) ret.Add(obj);
				} catch (Exception) { }
			}
			return ret;
		}


		private static Dictionary<string, SqliteParameter> GetParams(SqliteCommand cmd) {
			var ret = new Dictionary<string, SqliteParameter>();
			foreach (SqliteParameter i in cmd.Parameters) ret[i.ParameterName] = i;
			return ret;
		}

		private void Print() {
			System.Diagnostics.Debug.WriteLine(Query + (Params.Count > 0 ? " \n" + JsonSerializer.Serialize(Params.Values) : ""), "SQL");
		}

	}

}

