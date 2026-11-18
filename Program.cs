using System.Text;
using VisitorLog;

Console.OutputEncoding = Encoding.UTF8;

// dotnet run -- test  => รันชุดทดสอบ (ไม่ต้องต่อฐานข้อมูล)
if (args.Length > 0 && args[0] == "test")
    return Tests.Run();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

// ถ้าตั้ง ConnectionStrings:Supabase ไว้ จะเก็บข้อมูลใน Supabase
// ถ้าไม่ตั้ง จะใช้หน่วยความจำพร้อมข้อมูลตัวอย่าง (ปิดโปรแกรมแล้วหาย)
var connectionString = builder.Configuration.GetConnectionString("Supabase");
IVisitRepository repository;

if (string.IsNullOrWhiteSpace(connectionString))
{
    repository = new InMemoryVisitRepository();
    await DemoData.SeedAsync(new VisitorLogService(repository));
    Console.WriteLine("โหมดทดลอง: ไม่พบ ConnectionStrings:Supabase ข้อมูลอยู่ในหน่วยความจำและจะหายเมื่อปิดโปรแกรม");
}
else
{
    var postgres = new PostgresVisitRepository(connectionString);
    try
    {
        var count = await postgres.CountAsync();
        Console.WriteLine($"เชื่อมต่อ Supabase สำเร็จ (มี {count} รายการในตาราง visits)");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("เชื่อมต่อ Supabase ไม่สำเร็จ: " + ex.Message);
        Console.Error.WriteLine("ตรวจสอบสตริงเชื่อมต่อ และว่ารัน supabase/schema.sql แล้ว (ดู README.md)");
        return 1;
    }
    repository = postgres;
}

builder.Services.AddSingleton<IVisitRepository>(repository);
builder.Services.AddSingleton(new VisitorLogService(repository));

var app = builder.Build();
app.UseStaticFiles();
app.MapRazorPages();
await app.RunAsync();
return 0;
