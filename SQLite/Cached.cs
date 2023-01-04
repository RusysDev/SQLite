
// ----------------------------------------------------
//    SQLite Cache & Config
// ----------------------------------------------------
//  Database configuration and Cached items
// ----------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace RusysDev.SQLite {
	/// <summary>Cached database items base class for extensions</summary>
	/// <typeparam name="T">Object type to store items</typeparam>
	public class SqlCached<T> where T : new() {
		protected bool Running { get; set; }
		protected DateTime NextReload { get; set; }
		protected string Query { get; set; }
		/// <summary>Cache reload interval in seconds</summary>
		public int ReloadInterval { get; set; } = 5;
		private List<T> Cached { get; set; }
		public List<T> Items => Reload();
		public SqlCached(string sql) { Query = sql; Cached ??= new(); }

		/// <summary>Execution on item refresh</summary>
		/// <param name="items">Cached items list from database</param>
		public virtual void OnReload(List<T> items) { }

		/// <summary>Reload cached items from database</summary>
		/// <param name="force">Force update, skip time value</param>
		/// <returns>Cached item list</returns>
		public List<T> Reload(bool force = false) {
			while (Running) { Thread.Sleep(1); }
			if (NextReload < DateTime.UtcNow || force) {
				try {
					Running = true;
					Cached = new Sql("SELECT * FROM [Config]").GetData<T>();
					NextReload = DateTime.UtcNow.AddSeconds(ReloadInterval);
					OnReload(Cached);
				} catch (Exception) { } finally { Running = false; }
			}
			return Cached;
		}
	}

	namespace Config {
		/// <summary>Database Config table items class</summary>
		public class CfgItem {
			[SqlField("cfg_id")] public int ID { get; set; }
			[SqlField("cfg_key")] public string Key { get; set; } = "";
			[SqlField("cfg_name")] public string? Name { get; set; }
			[SqlField("cfg_value")] public string? Value { get; set; }
			[SqlField("cfg_num")] public long Number { get; set; }
			public int Int => unchecked((int)Number);
			[SqlField("cfg_data"), SqlJson] public object? Data { get; set; }
			public void Insert(string? descr = null) => SqlConfig.Insert(this, descr);
			public void Update(string? descr = null) => SqlConfig.Update(this, descr);
		}

		/// <summary>Cached database configuration</summary>
		public class SqlConfig : SqlCached<CfgItem> {
			private static readonly string Sql_Insert = "INSERT INTO [Config] (cfg_key,cfg_name,cfg_value,cfg_num,cfg_data,cfg_descr) VALUES ($cfg_key,$cfg_name,$cfg_value,$cfg_num,$cfg_data,$descr);";
			private static readonly string Sql_Update = "UPDATE [Config] SET cfg_value=$cfg_value,cfg_num=$cfg_num,cfg_data=$cfg_data,cfg_descr=COALESCE($descr,cfg_descr) WHERE cfg_id=$cfg_id or (cfg_key=$cfg_key and cfg_name=$cfg_name);";

			private Dictionary<string, CfgItem> CachedItems { get; set; } = new();
			private bool TryGetValue(string key, string name, [MaybeNullWhen(false)] out CfgItem value) { Reload(); return CachedItems.TryGetValue($"{key}|{name}", out value); }
			/// <summary>Method to get configuration item</summary>
			/// <param name="key">Key value</param>
			/// <param name="name">Name value</param>
			/// <returns>Configuration item</returns>
			public CfgItem? GetItem(string key, string name) => TryGetValue(key, name, out var item) ? item : null;
			/// <summary>Get configuration item string value from Value field</summary>
			/// <param name="key">Key value</param>
			/// <param name="name">Name value</param>
			/// <param name="default">Default value if null</param>
			/// <returns>Configuration item Value field value</returns>
			public string Val(string key, string name, string @default = "") => TryGetValue(key, name, out var item) ? (!string.IsNullOrEmpty(item.Value) ? item.Value : @default) : @default;
			/// <summary>Get integer value from configuration item</summary>
			/// <param name="key">Key value</param>
			/// <param name="name">Name value</param>
			/// <param name="default">Default value if configuration item is missing</param>
			/// <returns>Integer value</returns>
			public int Int(string key, string name, int @default = 0) => TryGetValue(key, name, out var item) ? (item.Number == 0 ? @default : (int)item.Number) : @default;
			public SqlConfig() : base("SELECT * FROM [Config]") { }
			public override void OnReload(List<CfgItem> items) {
				CachedItems = new(); foreach (var i in items) { CachedItems[$"{i.Key}|{i.Name}"] = i; }
			}

			public static void Insert(CfgItem item, string? descr = null) => new Sql(Sql_Insert, "$descr", descr).Execute(item);
			public static void Update(CfgItem item, string? descr = null) => new Sql(Sql_Update, "$descr", descr).Execute(item);
		}
	}
}
