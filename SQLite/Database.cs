// ------------------------------------------------
//          SQLite Database SDK main class
// ------------------------------------------------
//  Class for updating database from SqlUpdate.xml
// ------------------------------------------------

using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Data;

namespace RusysDev.SQLite {
	using Config;

	public class Sql {
		public static readonly bool Check = true;
		private static string DBFile { get; set; } = "";
		private static string? DbConn { get; set; }
		private static string DbConnStr => DbConn ?? Init();
		public static string FilePath { get => DBFile; set => Init(string.IsNullOrEmpty(value) ? "main.db" : value); }
		public static SqlConfig Config { get; } = new();
		public static string Init(string path = "main.db") { DbConn = $"Data Source={DBFile = path}"; new SqlUpdate().ProcessUpdates(); return DbConn; }

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
		private static SqliteConnection Conn() { var conn = new SqliteConnection(DbConnStr); conn.Open(); return conn; }
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
			var pr = SqlProps.Get<T>();
			return Read((rdr, ret) => ret.Add(pr.Fill<T>(rdr)), new List<T>());
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

		/// <summary>Execute Non Query</summary>
		/// <returns>Record count</returns>
		public int Execute() {
			using var conn = Conn();
			using var cmd = Cmd(conn);
			return cmd.ExecuteNonQuery();
		}

		/// <summary>Execute Non Query passing object as query parameters</summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="obj">Object to pass to parameters</param>
		/// <returns>Record count</returns>
		public int Execute<T>(T obj) {
			var pr = SqlProps.Get<T>();
			foreach (var i in pr) { Params.Add((i.Key, i.GetValue(obj))); }
			return Execute();
		}

		/// <summary>Execute Non Query for each object in list</summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="obj">List of objects</param>
		/// <returns>Record count</returns>
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

		/// <summary>Get list of single field objects from database</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field ID</param>
		/// <returns>List of objects</returns>
		public List<T> GetList<T>(int field = 0) => Read((rdr, ret) => ret.Add(rdr.GetFieldValue<T>(field)), new List<T>());

		/// <summary>Get list of single field objects from database</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field name</param>
		/// <returns>List of objects</returns>
		public List<T> GetList<T>(string field) => Read((rdr, ret) => ret.Add(rdr.GetFieldValue<T>(field)), new List<T>());


		/// <summary>Get single item from database</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field name</param>
		/// <returns>Value</returns>
		public T GetValue<T>(string field) where T : new() => Read((rdr, ret) => { ret = rdr.GetFieldValue<T>(field); return false; }, new T());
		/// <summary>Get single item from database</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field ID</param>
		/// <returns>Value</returns>
		public T GetValue<T>(int field = 0) where T : new() => Read((rdr, ret) => { ret = rdr.GetFieldValue<T>(field); return false; }, new T());



		/// <summary>Get list of items from database json column</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field ID</param>
		/// <returns>List of objects</returns>
		public List<T> GetListJson<T>(int field = 0) => Read((rdr, ret) => {
			try {
				var obj = JsonSerializer.Deserialize<T>(rdr.GetString(field));
				if (obj is not null) ret.Add(obj);
			} catch (Exception) { }
		}, new List<T>());


		/// <summary>Get dictionary of items</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field ID</param>
		/// <returns>Dictionary of objects</returns>
		public Dictionary<string, T> GetDict<T>(int field = 0) where T : new() {
			var pr = SqlProps.Get<T>();
			return Read((rdr, ret) => {
				var fld = rdr.GetString(field);
				if (fld is not null) ret[fld] = pr.Fill<T>(rdr);
			}, new Dictionary<string, T>());
		}

		/// <summary>Get dictionary of items</summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="field">Field name</param>
		/// <returns>Dictionary of objects</returns>
		public Dictionary<string, T> GetDict<T>(string field) where T : new() {
			var pr = SqlProps.Get<T>();
			return Read((rdr,ret) => {
				var fld = rdr.GetString(field);
				if (fld is not null) ret[fld] = pr.Fill<T>(rdr);
			}, new Dictionary<string, T>());
		}




		/// <summary>Execute reader and loop trough records</summary>
		/// <param name="func">Function for loop</param>
		public void Read(Action<SqliteDataReader> func) {
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) func(rdr);
		}

		/// <summary>Execute reader and loop trough records</summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="func">Function for loop</param>
		/// <param name="obj">Object to pass between loops</param>
		public T Read<T>(Action<SqliteDataReader, T> func, T obj) {
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) func(rdr, obj);
			return obj;
		}

		/// <summary>Execute reader and loop trough records</summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="func">Function for loop</param>
		/// <param name="obj">Object to pass between loops</param>
		public T Read<T>(Func<SqliteDataReader, T, bool> func, T obj) {
			using var conn = Conn();
			using var cmd = Cmd(conn);
			using var rdr = cmd.ExecuteReader();
			while (rdr.Read()) if (!func(rdr, obj)) break;
			return obj;
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

