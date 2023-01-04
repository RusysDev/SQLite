using Microsoft.Data.Sqlite;
using SQLite.Config;
using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;

namespace SQLite {

	public class Sql {
		private static readonly string Sql_Create = "CREATE TABLE Config (cfg_id INTEGER PRIMARY KEY AUTOINCREMENT, cfg_key TEXT(50), cfg_name TEXT(255), cfg_value TEXT(255) ,cfg_num INTEGER, cfg_data TEXT, cfg_descr TEXT);" +
			"INSERT INTO [Config] (cfg_key,cfg_name,cfg_value,cfg_num,cfg_descr) VALUES ('System','Version','v0',0,'Database version. Used to update database versions using SqlUpdate.xml');";
		private static string? DbConn { get; set; }
		public static string Database { get => DbConn ?? Init("main.db"); set { Init(value); } }

		public static SqlConfig Config { get; } = new ();

		private static string Init(string path) {
			DbConn = $"Data Source={path}";

			//Check if Config table exists in database and create it
			if (!GetTables().Contains("Config")) new Sql(Sql_Create).Execute();
			
			Config.Reload(true);
			var vers = Config.GetItem("System", "Version");

			var cfg = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "SqlUpdate.xml");
			if (File.Exists(cfg)) {

		//		using var rdr = new StreamReader(CfgFile = new FileInfo(cfg).FullName);
		//		var mtd = (Publishing?)new XmlSerializer(typeof(Publishing)).Deserialize(rdr);


			}
			//detect version

			//get config xml

			//"Data Source=hello.db"
			return DbConn;
		}

		public static List<string> GetTables() => new Sql("SELECT Name FROM sqlite_master WHERE type='table' and name not like 'sqlite_%'").GetList<string>();
		public static List<SqlColumn> GetFields(string table) => new Sql("SELECT * FROM PRAGMA_TABLE_INFO($tbl)","$tbl",table).GetData<SqlColumn>();


		public string Query { get; set; }
		public SqlParams Params { get; set; } = new();
		private SqliteTransaction? Tran { get; set; }
		private SqliteConnection Conn() { var conn = new SqliteConnection(Database); conn.Open(); return conn; }
		private SqliteCommand Cmd(SqliteConnection conn) {
			var cmd = new SqliteCommand(Query, conn, Tran);
			foreach (var i in Params) { cmd.Parameters.Add(new SqliteParameter(i.Item1, i.Item2)); }
			return cmd;
		}

		public Sql(string sql) { Query = sql; }
		public Sql(string sql, string param, object? value) { Query = sql; Params.Add(param, value); }
		public Sql(string sql, params ValueTuple<string, object?>[] param) { Query = sql; foreach (var i in param) { Params.Add(i); } }

		//	public DBParams(params ValueTuple<string, object?>[] pairs) { Data = pairs.ToDictionary(x => x.Item1, x => x.Item2); }

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


	}

}

