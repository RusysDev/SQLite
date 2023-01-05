using Microsoft.Data.Sqlite;
using System.Text.Json;

// ------------------------------------------------
//          SQLite Database SDK main class
// ------------------------------------------------
//  Class for updating database from SqlUpdate.xml
// ------------------------------------------------

namespace RusysDev.SQLite {
	using Config;

	public class Sql {
		public static readonly bool Check = true;
		private static string DBFile { get; set; } = "";
		private static string? DbConn { get; set; }
		private static string DbConnStr => DbConn ?? Init();
		public static string FilePath { get => DBFile; set => Init(string.IsNullOrEmpty(value) ? "main.db" : value); }
		public static SqlConfig Config { get; } = new();
		private static string Init(string path = "main.db") { DbConn = $"Data Source={DBFile = path}"; new SqlUpdate().ProcessUpdates(); return DbConn; }

		public static List<string> GetTables() => new Sql("SELECT Name FROM sqlite_master WHERE type='table' and name not like 'sqlite_%'").GetList<string>();
		public static List<SqlColumn> GetFields(string table) => new Sql("SELECT * FROM PRAGMA_TABLE_INFO($tbl)", "$tbl", table).GetData<SqlColumn>();

		/// <summary>Create backup of SQLite database file</summary>
		/// <param name="vers">
		/// Version name (default:null)
		/// Format: {dbfile}.{version}.bak</param>
		/// <param name="keep"></param>
		/// <returns></returns>
		public static string Backup(string? vers = null, int keep = 0) {
			var fl = FilePath;
			var bk = $"{fl}.{(string.IsNullOrEmpty(vers) ? "" : $"{vers}.")}bak";
			File.Copy(fl, bk, true);
			//Perform backup cleanup
			if (keep > 0 && vers is not null) {
				var dir = Path.GetDirectoryName(new FileInfo(fl).FullName);
				if (dir is not null) {
					var baks = new DirectoryInfo(dir).GetFiles($"*.db.*.bak", SearchOption.TopDirectoryOnly);
					foreach (var file in baks.OrderByDescending(file => file.CreationTime).Skip(keep)) { file.Delete(); }
				}
			}
			return bk;
		}

		public string Query { get; set; }
		public SqlParams Params { get; }
		/// <summary>Sql Query timeout in seconds</summary>
		public int Timeout { get; set; } = 10;
		private SqliteTransaction? Tran { get; set; }
		private SqliteConnection Conn() { var conn = new SqliteConnection(DbConnStr); conn.Open(); return conn; }
		private SqliteCommand Cmd(SqliteConnection conn) {
			var cmd = new SqliteCommand(Query, conn, Tran) { CommandTimeout = Timeout > 0 ? Timeout : 10 };
			cmd.Parameters.Add(new("$now", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")));
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

		/// <summary>Get rows from database formatted as selected class</summary>
		/// <typeparam name="T">Class type for items</typeparam>
		/// <returns>List of T objects</returns>
		public List<T> GetData<T>() where T : new() {
			var ret = new List<T>();
			var pr = SqlProps.Get<T>();
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) ret.Add(pr.Fill<T>(rdr));
			return ret;
		}

		/// <summary>Get single row from database formatted as selected class</summary>
		/// <typeparam name="T">Class type for row</typeparam>
		/// <returns>Row in database as T</returns>
		public T? GetItem<T>() where T : new() {
			var pr = SqlProps.Get<T>();
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			return rdr.Read() ? pr.Fill<T>(rdr) : default;
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

