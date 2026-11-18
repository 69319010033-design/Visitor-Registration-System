# ทะเบียนผู้มาติดต่อสำนักงาน (C# + Supabase)

เว็บ ASP.NET Core Razor Pages เก็บข้อมูลใน Supabase (Postgres)
ถ้าไม่ตั้งค่า Supabase จะรันในโหมดทดลอง ใช้หน่วยความจำและข้อมูลตัวอย่าง

## ตั้งค่า Supabase

1. สร้างโปรเจกต์ที่ https://supabase.com
2. เปิด **SQL Editor** วางเนื้อหา `supabase/schema.sql` ทั้งหมด แล้วกด Run
   (ตารางจะบังคับกติกาเวลาออก ≥ เวลาเข้า และการยกเลิกต้องมีเหตุผลอีกชั้นหนึ่ง)
3. ถ้าอยากได้ข้อมูลตัวอย่าง รัน `supabase/seed.sql` ด้วย
4. เปิดปุ่ม **Connect** ในโปรเจกต์ แล้วคัดลอกสตริงเชื่อมต่อ
   - เครือข่ายที่รองรับ IPv6: ใช้ *Direct connection* (พอร์ต 5432)
   - เครือข่ายที่ใช้ได้แต่ IPv4: ใช้ *Session pooler* (พอร์ต 5432 ชื่อผู้ใช้ `postgres.<project-ref>`)
   - ไม่ควรใช้ Transaction pooler (พอร์ต 6543) กับแอปที่รันค้างตลอด
5. ใส่สตริงเชื่อมต่อโดย **ไม่เขียนลงในไฟล์ที่เก็บในโค้ด** เลือกวิธีใดวิธีหนึ่ง

```bash
# วิธี A: user-secrets (แนะนำตอนพัฒนา)
dotnet user-secrets set "ConnectionStrings:Supabase" "postgresql://postgres.xxxx:รหัสผ่าน@host:5432/postgres"

# วิธี B: ตัวแปรสภาพแวดล้อม (Linux/macOS)
export ConnectionStrings__Supabase="postgresql://postgres.xxxx:รหัสผ่าน@host:5432/postgres"
# Windows PowerShell:  $env:ConnectionStrings__Supabase = "postgresql://..."
```

ถ้ารหัสผ่านมีอักขระพิเศษ (เช่น `@` `:` `/`) ต้องเข้ารหัสแบบ URL ก่อนใส่ในสตริงแบบ URI
หรือใช้รูปแบบ Npgsql แทน: `Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`

## รัน

```bash
dotnet run
# เปิด http://localhost:5080
```

เมื่อเชื่อมต่อสำเร็จ หน้าจอจะพิมพ์ "เชื่อมต่อ Supabase สำเร็จ" ถ้าไม่สำเร็จจะบอกสาเหตุและหยุดทำงาน

## ทดสอบ

```bash
dotnet run -- test
```

ชุดทดสอบใช้ที่เก็บในหน่วยความจำ ไม่แตะฐานข้อมูลจริง

## โครงสร้าง

- `VisitorLogService.cs` กติกาของระบบ
- `IVisitRepository.cs` ข้อกำหนดของที่เก็บข้อมูล
- `PostgresVisitRepository.cs` เก็บใน Supabase / `InMemoryVisitRepository.cs` เก็บในหน่วยความจำ
- `Pages/` หน้าเว็บ, `wwwroot/css/style.css` หน้าตา
- `supabase/` สคริปต์ SQL
