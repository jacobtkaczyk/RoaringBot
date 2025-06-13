var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Console.WriteLine("C# backend is running inside Docker!");

app.MapGet("/", () => "Hello World!");

app.Run();
