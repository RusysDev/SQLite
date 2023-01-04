using SQLite.Config;
using System.Reflection;
using System.Xml.Serialization;

namespace SQLite {
	using Updates;
	/// <summary>Database update class</summary>
	public class SqlUpdate {
		private static readonly string Sql_Create = "CREATE TABLE Config (cfg_id INTEGER PRIMARY KEY AUTOINCREMENT, cfg_key TEXT(50), cfg_name TEXT(255), cfg_value TEXT(255) ,cfg_num INTEGER, cfg_data TEXT, cfg_descr TEXT);";

		public List<SqlUpdItem> Updates { get; set; } = new();
		public CfgItem Version { get; private set; }
		public string Config { get; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "SqlUpdate.xml");

		public SqlUpdate() {
			//Check if Config table exists in database and create it
			if (!Sql.GetTables().Contains("Config")) new Sql(Sql_Create).Execute();
			Sql.Config.Reload(true);

			//Get version from configuration table
			var vers = Sql.Config.GetItem("System", "Version");
			if (vers is null) {
				//Add version if does not exist;
				Version = new CfgItem() { Key = "System", Name = "Version", Value = "None", Number = 0 };
				Version.Insert("Database version. Used to update database versions using SqlUpdate.xml");
				Sql.Config.Reload(true);
			}
			else Version = vers;

			//Try get xml file
			if (File.Exists(Config)) {
				using var rdr = new StreamReader(Config);
				var mtd = (SqlUpdList?)new XmlSerializer(typeof(SqlUpdList)).Deserialize(rdr);
				var vint = Version.Int;
				if (mtd is not null) {
					foreach (var i in mtd.Updates) if (i.Version > vint && !string.IsNullOrEmpty(i.Query)) Updates.Add(i);
					Updates = Updates.OrderBy(x=>x.Version).ToList();
				}
			}
		}
		
		/// <summary>Execute and print out database updates</summary>
		public void PrintUpdates() {
			if (Updates.Count > 0) {
				Console.WriteLine($"Updating database.\nCurrent: v{Version.Number} - {Version.Name}");
				var sts = Execute();
				foreach (var i in sts) { Console.WriteLine($"Update: v{i.Version} - {i.Name} ({i.ExecTime / 1000}s)"); }
				if (sts.Error) {
					Console.WriteLine($"Error updating database: {sts.Message}");
					throw new(sts.Message);
				}
			}
		}

		public SqlUpdExec Execute() {
			var tmr = System.Diagnostics.Stopwatch.StartNew();
			var ret = new SqlUpdExec();
			try {
				var inc = 1;
				foreach (var i in Updates) {
					if (!string.IsNullOrEmpty(i.Query)) {
						var sw = System.Diagnostics.Stopwatch.StartNew();
						new Sql(i.Query).Execute();
						Version.Number = i.Version; Version.Value = i.Name;
						ret.Add(new() {
							Number = inc++, Version = i.Version, Name = i.Name,
							ExecTime = sw.ElapsedMilliseconds
						});
					}
				}
				Version.Update();
				File.Delete(Config);

			} catch (Exception ex) { ret.Error = true; ret.Message = ex.Message; }
			ret.ExecTime = tmr.ElapsedMilliseconds;
			return ret;
		}

	}

	namespace Updates {
		[XmlRoot("SqlUpdates")]
		public class SqlUpdList {
			[XmlElement("SqlUpdate")] public List<SqlUpdItem> Updates { get; set; } = new();
		}
		public class SqlUpdItem {
			[XmlAttribute] public string? Name { get; set; }
			[XmlAttribute] public int Version { get; set; }
			public string? Query { get; set; }
		}
		public class SqlUpdExec : List<SqlUpdStatus> {
			public bool Error { get; set; }
			public string? Message { get; set; } = "Success";
			public long ExecTime { get; set; }
		}
		public class SqlUpdStatus {
			public int Number { get; set; }
			public int Version { get; set; }
			public string? Name { get; set; }
			public long ExecTime { get; set; }
		}
	}

}
