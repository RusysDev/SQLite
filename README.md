# SQLite 

Usage: 
```cs
new Sql("UPDATE [Table] SET [Field]=$val;", "$val", "somevalue").Execute();
```

# Using in VisualStudio NuGet

Update: `%AppData%\NuGet\NuGet.Config`
```XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<packageSources>
		<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
		<add key="RusysDev" value="https://nuget.pkg.github.com/RusysDev/index.json" />
	</packageSources>
	<packageSourceCredentials>
		<RusysDev>
			<add key="Username" value="RusysDev" />
			<add key="ClearTextPassword" value="[Personal_Token]" />
		</RusysDev>
	</packageSourceCredentials>
</configuration>
```
