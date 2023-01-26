// ------------------------------------------------------
//                SQLite extensions 
// ------------------------------------------------------
//  This code is used to simplify general SQLite classes
// ------------------------------------------------------

using Microsoft.Data.Sqlite;

namespace RusysDev.SQLite {
	public static class Extensions {
		public static bool IsList(this object o) {
			if (o == null) return false;
			if (o is Type type) { return type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)); }
			return o is System.Collections.IList && o.GetType().IsGenericType && o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
		}
		public static bool IsDictionary(this object o) {
			if (o == null) return false;
			if (o is Type type) { return type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>)); }
			return o is System.Collections.IDictionary && o.GetType().IsGenericType && o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
		}
		public static object? ChangeType(this object? value, Type t) {
			if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>))) {
				if (value == null) { return null; }
				t = Nullable.GetUnderlyingType(t) ?? typeof(string);
			}
			return Convert.ChangeType(value, t);
		}
		public static string? EmptyToNull(this object o) => string.IsNullOrEmpty(o?.ToString()) ? null : o.ToString();
		public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new() {
			if (!dict.TryGetValue(key, out var val)) { val = new TValue(); dict.Add(key, val); }
			return val;
		}

		public static string[] GetFields(this SqliteDataReader rdr) {
			var ret = new List<string>();
			for (int i = 0, j = rdr.FieldCount; i < j; i++) ret.Add(rdr.GetName(i));
			return ret.ToArray();
		}
	}
}