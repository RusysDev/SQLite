using System.Xml.Serialization;

// ------------------------------------------------
//        SQLite Database version update
// ------------------------------------------------
//  Class for updating database from SqlUpdate.xml
// ------------------------------------------------

namespace RusysDev.SQLite {
	using Updates;
	using Config;

	/// <summary>Database update class</summary>
	public class SqlUpdate {
		private static readonly string Sql_Create = "CREATE TABLE Config (cfg_id INTEGER PRIMARY KEY AUTOINCREMENT, cfg_key TEXT(50), cfg_name TEXT(255), cfg_value TEXT(255) ,cfg_num INTEGER, cfg_data TEXT, cfg_descr TEXT);";

		public List<SqlUpdItem> Updates { get; set; } = new();
		public CfgItem Version { get; private set; }
		public string Config { get; } = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "SqlUpdate.xml");
		
		public SqlUpdate() {
			//Check if Config table exists in database and create it
			if (!Sql.GetTables().Contains("Config")) { new Sql(Sql_Create).Execute(); }

			//Get version from configuration table
			var vers = new Sql("SELECT * FROM [Config] WHERE [cfg_key]='System' and [cfg_name]='Version';").GetItem<CfgItem>();
			if (vers is null) {
				//Add version if does not exist;
				Version = new CfgItem() { Key = "System", Name = "Version", Value = "None", Number = 0 };
				Version.Insert("Database version. Used to update database versions using SqlUpdate.xml");
				Sql.Config.Reload(true);
			}
			else Version = vers;

			//Try get xml file
			if (File.Exists(Config)) {
				using (var rdr = new StreamReader(Config)) {
					var mtd = (SqlUpdList?)new XmlSerializer(typeof(SqlUpdList)).Deserialize(rdr);
					var vint = Version.Int;
					if (mtd is not null) {
						foreach (var i in mtd.Updates) if (i.Version > vint && i.Query?.Count > 0) Updates.Add(i);
						Updates = Updates.OrderBy(x => x.Version).ToList();
					}
				}
				if (Updates.Count == 0) File.Delete(Config);
			}
		}
		
		/// <summary>Execute and print out database updates</summary>
		public void ProcessUpdates(bool print=true) {
			if (Updates.Count > 0) {
				if (print) Console.WriteLine($"Updating database.\nCurrent: v{Version.Number} - {Version.Name}");
				var sts = Execute();
				foreach (var i in sts) { if (print) Console.WriteLine($"Update: v{i.Version} - {i.Name} ({(float)i.ExecTime / 1000}s)"); if (sts.Error) { break; } }
				if (sts.Error) {
					if (print) Console.WriteLine($"Error updating database: {sts.Message}");
					throw new(sts.Message);
				}
			}
		}


		public SqlUpdExec Execute() {
			var tmr = System.Diagnostics.Stopwatch.StartNew();
			var ret = new SqlUpdExec(); int vers = (int)Version.Number;
			try {
				Sql.Backup($"update.v{Version.Number}");
				var inc = 1;
				foreach (var i in Updates) {
					if (i.Query?.Count > 0) {
						var sw = System.Diagnostics.Stopwatch.StartNew();
						foreach (var j in i.Query) new Sql(j).Execute();
						ret.Add(new() {
							Number = inc++, Version = i.Version, Name = i.Name,
							ExecTime = sw.ElapsedMilliseconds
						});
						Version.Number = vers = i.Version; Version.Value = i.Name; Version.Update();
						File.AppendAllText("sqlpdate.log", $"Update: v{i.Version} - {i.Name} ({(double)sw.ElapsedMilliseconds / 1000}s)" + Environment.NewLine);
					}
				}
				File.Delete(Config);

			} catch (Exception ex) {
				ret.Error = true; ret.Message = ex.Message;
				File.AppendAllText("sqlpdate.log", $"Error: v{vers} - {ex.Message}" + Environment.NewLine);
			}
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
			[XmlElement] public List<string>? Query { get; set; }
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
