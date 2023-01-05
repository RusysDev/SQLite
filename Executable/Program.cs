
using RusysDev.SQLite;
using System.Reflection;

Console.WriteLine("Hello, World!");

var tsk = new List<Task>();
var sq = System.Diagnostics.Stopwatch.StartNew();
Console.WriteLine("Connstart "+ sq.ElapsedMilliseconds/(float)1000);

var m = Sql.Config.Items;

Sql.Database = "hello.db";

////var c = Sql.Config;

//Console.WriteLine(Sql.GetTables().Count);
//var f = Sql.GetFields("Config");
//Console.WriteLine(f.Count);

//new SQLite.Sql("INSERT INTO [test] (name,value) VALUES ($name,$Value)", "$prm", "aaa")
//	.Execute(new List<Dta>() { new() { Name = "In1", Value = new() { Text = "text1" } }, new() { Name = "In2", Value = new() { Text = "text2" } } });

//for (var i = 0; i < 1; i++) {
//	tsk.Add(Task.Run(() => { Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new SQLite.Sql("select * from [test] where $prm='aaa';", "$prm","aaa").GetData<Dta>())); }));
//}
//Console.WriteLine("Waiting for tasks "+ sq.ElapsedMilliseconds/(float)1000);
//Task.WaitAll(tsk.ToArray());
//Console.WriteLine("Done "+ sq.ElapsedMilliseconds/(float)1000);



Console.Write("\nDone. ");
Console.ReadLine();



//public class Dta {
//	[SqlField(0)] public int Id { get; set; }
//	[SqlField(1, "name")] public string? Name { get; set; }
//	[SqlField(2), SqlJson] public Jsn? Value { get; set; }
//}

//public class Jsn {
//	public int Id { get; set; }
//	public string? Text { get; set; }
//}