using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

// -----------------------
//    SQLite Properties
// -----------------------
//  Properties and attributes for main code
// -----------------------

namespace SQLite {
	/// <summary>Sql parameter collection</summary>
	public class SqlParams : List<ValueTuple<string, object?>> {
		/// <summary>Get all parameters as Key/Value dictionary object</summary>
		public Dictionary<string, object?> Values => this.ToDictionary(x => x.Item1, y => y.Item2);
		/// <summary>Add keu/value pair to parameter collection</summary>
		/// <param name="key">Parameter key (Starts with $)</param>
		/// <param name="value">Parameter value</param>
		public void Add(string key, object? value) => Add(new(key, value));
	}

	/// <summary>Sql query property item</summary>
	public class SqlProp {
		public static HashSet<Type> Types = new() { typeof(int), typeof(long), typeof(bool), typeof(DateTime) };

		private void SetString(object obj, object? val) => Prop.SetValue(obj, val?.EmptyToNull());
		private void SetData(object obj, object? val) => Prop.SetValue(obj, Convert.ChangeType(val, Prop.PropertyType));
		private void SetEnum(object obj, object? val) => Prop.SetValue(obj, Enum.Parse(Prop.PropertyType, val?.ToString() ?? ""));
		private void SetJson(object obj, object? val) { try { var jsn = val is not null ? JsonSerializer.Deserialize(val.ToString() ?? "", Prop.PropertyType) : null; Prop.SetValue(obj, jsn); } catch (Exception) { } }
		private void SetTry(object obj, object? val) { try { Prop.SetValue(obj, val?.ChangeType(Prop.PropertyType)); } catch (Exception) { SetJson(obj, val); } }

		/// <summary>Property ID</summary>
		public int ID { get; set; }
		/// <summary>Property Name</summary>
		public string Name { get; set; }
		/// <summary>Property Name in SQLite format ($ + Property Name)</summary>
		public string Key { get; }
		/// <summary>Property Info</summary>
		public PropertyInfo Prop { get; }
		/// <summary>Json indicator for parsing data from/to Json object</summary>
		public bool Json { get; set; }
		/// <summary>Action for setting property value, used for different type parsing</summary>
		public Action<object, object?> SetValue { get; set; }
		/// <summary>Get property value for storing it to Database</summary>
		/// <param name="obj">Object value</param>
		/// <returns>Value of object for storing to Datbase</returns>
		public object? GetValue(object? obj) { var val = Prop.GetValue(obj); return Json && val is not null ? JsonSerializer.Serialize(val) : val; }

		public SqlProp(SqlField fld, PropertyInfo prop) {
			Name = fld.Name ?? prop.Name; ID = fld.ID; Prop = prop;
			var tp = prop.PropertyType; Key = "$" + Name;
			Json = prop.GetCustomAttribute(typeof(SqlJson)) is not null;
			if (tp == typeof(string)) { SetValue = SetString; }
			else if (Types.Contains(tp)) { SetValue = SetData; }
			else if (tp.IsEnum) { SetValue = SetEnum; }
			else if (tp.IsList() || tp.IsDictionary()) { SetValue = SetJson; }
			else if (Json) { SetValue = SetJson; }
			else { SetValue = SetTry; }
		}
	}

	public static class SqlProps {
		private static ConcurrentDictionary<string, List<SqlProp>> List { get; set; } = new();
		public static List<SqlProp> Get<T>() {
			var tp = typeof(T);
			var nme = tp.FullName ?? tp.ToString();
			if (List.TryGetValue(nme, out var itm)) { return itm; }
			else {
				var lst = new List<SqlProp>();
				var props = tp.GetProperties();
				foreach (PropertyInfo prop in props)
					foreach (var attr in prop.GetCustomAttributes(typeof(SqlField)))
						if (attr is SqlField vl) lst.Add(new(vl, prop));
				var ret = List[nme] = lst.OrderBy(x => x.ID).ToList();
				return ret;
			}
		}
		public static T Fill<T>(this List<SqlProp> lst, SqliteDataReader rdr) where T : new() {
			var ret = new T();
			foreach (var i in lst) { i.SetValue(ret, i.ID > 0 ? rdr[i.ID] : rdr[i.Name]); }
			return ret;
		}
	}

	[AttributeUsage(AttributeTargets.Property)] public class SqlJson : Attribute { }
	[AttributeUsage(AttributeTargets.Property)]
	public class SqlField : Attribute {
		public int ID { get; set; }
		public string? Name { get; set; }
		public SqlField(int id) { ID = id; }
		public SqlField(int id, string? name) { ID = id; Name = name; }
		public SqlField(string? name) { ID = -1; Name = name; }
	}

	public class SqlColumn {
		[SqlField("cid")] public int ID { get; set; }
		[SqlField("name")] public string? Name { get; set; }
		[SqlField("type")] public string? Type { get; set; }
		[SqlField("notnull")] public bool NotNull { get; set; }
		[SqlField("dflt_value")] public string? Default { get; set; }
		[SqlField("pk")] public bool PK { get; set; }
	}
}
