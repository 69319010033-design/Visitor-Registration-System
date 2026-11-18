namespace VisitorLog;

/// <summary>ชุดทดสอบแบบไม่พึ่งแพ็กเกจภายนอก ใช้ที่เก็บในหน่วยความจำ รันด้วย: dotnet run -- test</summary>
public static class Tests
{
    private static int _passed;
    private static int _failed;

    private static readonly DateTime T0 = new(2026, 11, 18, 9, 0, 0);

    private static VisitorLogService NewService(Func<DateTime>? clock = null) =>
        new(new InMemoryVisitRepository(), clock);

    public static int Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        await Case("เวลาออก = เวลาเข้า ต้องอนุญาต", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ หนึ่ง", "หน่วยงาน ก", "คุณเอ", "ส่งเอกสาร", T0);
            await s.CheckOutAsync(v.Id, T0);
            var saved = (await s.FindAsync(v.Id))!;
            Eq(VisitStatus.CheckedOut, saved.Status);
            Eq(T0, saved.CheckOut);
        });

        await Case("เวลาออกหลังเวลาเข้า ต้องอนุญาต", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ สอง", "หน่วยงาน ข", "คุณบี", "ประชุม", T0);
            await s.CheckOutAsync(v.Id, T0.AddMinutes(90));
            Eq(VisitStatus.CheckedOut, (await s.FindAsync(v.Id))!.Status);
        });

        await Case("เวลาออกน้อยกว่าเวลาเข้า 1 วินาที ต้องปฏิเสธ และสถานะต้องไม่เปลี่ยน", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ สาม", "หน่วยงาน ค", "คุณซี", "ติดต่อสอบถาม", T0);
            await Throws<ArgumentException>(() => s.CheckOutAsync(v.Id, T0.AddSeconds(-1)));
            var saved = (await s.FindAsync(v.Id))!;
            Eq(VisitStatus.Inside, saved.Status);
            True(saved.CheckOut is null, "CheckOut ต้องยังเป็น null");
        });

        await Case("เวลาออกคนละวัน ก่อนวันเข้า ต้องปฏิเสธ", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ สี่", "หน่วยงาน ง", "คุณดี", "ส่งของ", T0);
            await Throws<ArgumentException>(() => s.CheckOutAsync(v.Id, T0.AddDays(-1)));
        });

        await Case("ออกซ้ำ ต้องปฏิเสธ", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ ห้า", "หน่วยงาน จ", "คุณอี", "ประชุม", T0);
            await s.CheckOutAsync(v.Id, T0.AddHours(1));
            await Throws<InvalidOperationException>(() => s.CheckOutAsync(v.Id, T0.AddHours(2)));
        });

        await Case("ข้อมูลหลักว่างหรือเว้นวรรคอย่างเดียว ต้องปฏิเสธ", async () =>
        {
            var s = NewService();
            await Throws<ArgumentException>(() => s.CheckInAsync("", "หน่วยงาน", "คุณเอ", "ประชุม", T0));
            await Throws<ArgumentException>(() => s.CheckInAsync("ชื่อ", "   ", "คุณเอ", "ประชุม", T0));
            await Throws<ArgumentException>(() => s.CheckInAsync("ชื่อ", "หน่วยงาน", "", "ประชุม", T0));
            await Throws<ArgumentException>(() => s.CheckInAsync("ชื่อ", "หน่วยงาน", "คุณเอ", " ", T0));
            Eq(0, (await s.GetAllAsync()).Count);
        });

        await Case("ยกเลิกต้องมีเหตุผล (ว่าง/เว้นวรรค/null ปฏิเสธ)", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ หก", "หน่วยงาน ฉ", "คุณเอฟ", "ประชุม", T0);
            await Throws<ArgumentException>(() => s.CancelAsync(v.Id, ""));
            await Throws<ArgumentException>(() => s.CancelAsync(v.Id, "   "));
            await Throws<ArgumentException>(() => s.CancelAsync(v.Id, null!));
            Eq(VisitStatus.Inside, (await s.FindAsync(v.Id))!.Status);
        });

        await Case("ยกเลิกแล้วรายการยังอยู่ในระบบ พร้อมเหตุผล และไม่นับว่าอยู่ในสำนักงาน", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ เจ็ด", "หน่วยงาน ช", "คุณจี", "ประชุม", T0);
            await s.CancelAsync(v.Id, "กรอกข้อมูลผิดคน", T0.AddMinutes(5));
            var saved = (await s.FindAsync(v.Id))!;
            Eq(1, (await s.GetAllAsync()).Count);
            Eq(VisitStatus.Cancelled, saved.Status);
            Eq("กรอกข้อมูลผิดคน", saved.CancelReason);
            Eq(0, (await s.GetInsideAsync()).Count);
            Eq(1, (await s.GetCancelledAsync()).Count);
        });

        await Case("ยกเลิกซ้ำ หรือออกหลังยกเลิก ต้องปฏิเสธ", async () =>
        {
            var s = NewService();
            var v = await s.CheckInAsync("ทดสอบ แปด", "หน่วยงาน ซ", "คุณเอช", "ประชุม", T0);
            await s.CancelAsync(v.Id, "ลงทะเบียนซ้ำ");
            await Throws<InvalidOperationException>(() => s.CancelAsync(v.Id, "เหตุผลอื่น"));
            await Throws<InvalidOperationException>(() => s.CheckOutAsync(v.Id, T0.AddHours(1)));
        });

        await Case("ไม่พบรหัสรายการ ต้องแจ้งข้อผิดพลาด", async () =>
        {
            var s = NewService();
            await Throws<KeyNotFoundException>(() => s.CheckOutAsync(999, T0));
            await Throws<KeyNotFoundException>(() => s.CancelAsync(999, "เหตุผล"));
        });

        await Case("รายชื่อ 'ยังอยู่' และ 'ออกแล้ว' แยกถูกต้อง", async () =>
        {
            var s = NewService();
            var a = await s.CheckInAsync("เอ", "หน่วยงาน 1", "คุณเอ", "ประชุม", T0);
            var b = await s.CheckInAsync("บี", "หน่วยงาน 2", "คุณบี", "ส่งของ", T0.AddMinutes(10));
            var c = await s.CheckInAsync("ซี", "หน่วยงาน 3", "คุณซี", "ยื่นเรื่อง", T0.AddMinutes(20));
            await s.CheckOutAsync(a.Id, T0.AddMinutes(30));
            await s.CancelAsync(c.Id, "ไม่ได้เข้ามาจริง");

            Eq(new[] { b.Id }, (await s.GetInsideAsync()).Select(v => v.Id).ToArray());
            Eq(new[] { a.Id }, (await s.GetCheckedOutAsync()).Select(v => v.Id).ToArray());
            Eq(new[] { c.Id }, (await s.GetCancelledAsync()).Select(v => v.Id).ToArray());
            Eq(3, (await s.GetAllAsync()).Count);
        });

        await Case("ใช้นาฬิการะบบ (inject) เมื่อไม่ระบุเวลา", async () =>
        {
            var now = T0;
            var s = NewService(() => now);
            var v = await s.CheckInAsync("ทดสอบ เก้า", "หน่วยงาน ญ", "คุณเจ", "ประชุม");
            Eq(T0, v.CheckIn);
            now = T0.AddHours(2);
            await s.CheckOutAsync(v.Id);
            Eq(T0.AddHours(2), (await s.FindAsync(v.Id))!.CheckOut);
        });

        Console.WriteLine();
        Console.WriteLine($"ผลทดสอบ: ผ่าน {_passed}, ไม่ผ่าน {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    // ---------- ตัวช่วยทดสอบ ----------

    private static async Task Case(string name, Func<Task> body)
    {
        try
        {
            await body();
            _passed++;
            Console.WriteLine($"[ผ่าน]    {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"[ไม่ผ่าน] {name}");
            Console.WriteLine($"          {ex.Message}");
        }
    }

    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        catch (Exception ex)
        {
            throw new Exception($"คาดว่าจะเกิด {typeof(T).Name} แต่เกิด {ex.GetType().Name}");
        }
        throw new Exception($"คาดว่าจะเกิด {typeof(T).Name} แต่ไม่เกิดข้อผิดพลาด");
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Eq<T>(T expected, T actual)
    {
        if (expected is Array ea && actual is Array aa)
        {
            if (!ea.Cast<object?>().SequenceEqual(aa.Cast<object?>()))
                throw new Exception("อาร์เรย์ไม่ตรงกัน");
            return;
        }
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"คาดว่า <{expected}> แต่ได้ <{actual}>");
    }
}
