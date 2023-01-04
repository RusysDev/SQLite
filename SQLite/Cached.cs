
// ----------------------------------------------------
//    SQLite Cache & Config
// ----------------------------------------------------
//  Database configuration and Cached items
// ----------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace SQLite {
	public class SqlCached<T> where T : new() {
		protected bool Running { get; set; }
		protected DateTime NextReload { get; set; }
		protected string Query { get; set; }
		/// <summary>Cache reload interval in seconds</summary>
		public int ReloadInterval { get; set; } = 5;
		private List<T> Cached { get; set; }
		public List<T> Items => Reload();
		public SqlCached(string sql) { Query = sql; Cached ??= new(); }

		public virtual void OnReload(List<T> items) { }

		public List<T> Reload(bool force = false) {
			Console.WriteLine("Base: Start Reload");
			while (Running) { Thread.Sleep(1); }
			if (NextReload < DateTime.UtcNow || force) {
				try {
					Running = true;
					Cached = new Sql("SELECT * FROM [Config]").GetData<T>();
					NextReload = DateTime.UtcNow.AddSeconds(ReloadInterval); 
					OnReload(Cached);
				} catch (Exception) { } finally { Running = false; }
			}
			Console.WriteLine("Base: End Reload");
			return Cached;
		}
	}


	namespace Config {
		public class CfgItem {
			[SqlField("cfg_id")] public int ID { get; set; }
			[SqlField("cfg_key")] public string Key { get; set; } = "";
			[SqlField("cfg_name")] public string? Name { get; set; }
			[SqlField("cfg_value")] public string? Value { get; set; }
			[SqlField("cfg_num")] public long Number { get; set; }
			[SqlField("cfg_data"), SqlJson] public object? Data { get; set; }
		}
		public class SqlConfig : SqlCached<CfgItem> {
			private Dictionary<string, CfgItem> CachedItems { get; set; } = new();
			private bool TryGetValue(string key, string name, [MaybeNullWhen(false)] out CfgItem value) {
				Reload(); return CachedItems.TryGetValue($"{key}|{name}", out value);
			}
			public CfgItem? GetItem(string key, string name) => TryGetValue(key, name, out var item) ? item : null;
			public string Val(string key, string name, string @default = "") => TryGetValue(key, name, out var item) ? (!string.IsNullOrEmpty(item.Value) ? item.Value : @default) : @default;
			public int Int(string key, string name, int @default = 0) => TryGetValue(key, name, out var item) ? (item.Number == 0 ? @default : (int)item.Number) : @default;

			public SqlConfig() : base("SELECT * FROM [Config]") {}

			public override void OnReload(List<CfgItem> items) {
				Console.WriteLine("OnReload: Start");
				CachedItems = new();
				foreach (var i in items) { CachedItems[$"{i.Key}|{i.Name}"] = i; }
				Console.WriteLine("OnReload: End");
			}
		}
	}
}
