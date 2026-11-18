namespace VisitorLog;

/// <summary>ข้อมูลบุคคลสมมติสำหรับโหมดทดลอง (เวลาอิงจากวันนี้)</summary>
public static class DemoData
{
    public static async Task SeedAsync(VisitorLogService s)
    {
        var today = DateTime.Today;
        DateTime At(int hour, int minute) => today.AddHours(hour).AddMinutes(minute);

        // ออกแล้ว
        var v1 = await s.CheckInAsync("สมชาย ใจดี", "บริษัท ตัวอย่างวิศวกรรม จำกัด", "คุณอรทัย (ฝ่ายบุคคล)",
                                      "นัดหมายเรื่องสัญญาจ้าง", At(8, 30));
        await s.CheckOutAsync(v1.Id, At(10, 0));

        // ฝากเอกสาร เข้า-ออกเวลาเดียวกัน (กรณีเท่ากัน ซึ่งอนุญาต)
        var v2 = await s.CheckInAsync("สุดา รักเรียน", "สำนักงานเขตสมมติ", "คุณวิชัย (ธุรการ)",
                                      "ส่งหนังสือราชการ", At(9, 15));
        await s.CheckOutAsync(v2.Id, At(9, 15));

        // ยังอยู่ในสำนักงาน
        await s.CheckInAsync("ประเสริฐ ทองดี", "บริษัท เอบีซี ซัพพลาย จำกัด", "คุณนภา (ฝ่ายจัดซื้อ)",
                             "เสนอราคาสินค้า", At(10, 20));
        await s.CheckInAsync("มาลี สายสุข", "มหาวิทยาลัยสมมติ", "ผอ.กิตติ (ฝ่ายวิชาการ)",
                             "ประชุมความร่วมมือ", At(10, 45));

        // ยกเลิกรายการ (ลงทะเบียนผิดคน) พร้อมเหตุผล
        var v5 = await s.CheckInAsync("วีระ มั่นคง", "บริษัท ทดสอบ จำกัด", "คุณอรทัย (ฝ่ายบุคคล)",
                                      "สัมภาษณ์งาน", At(11, 0));
        await s.CancelAsync(v5.Id, "เจ้าหน้าที่กรอกชื่อผิดคน", At(11, 2));
    }
}
