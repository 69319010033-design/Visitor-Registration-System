namespace VisitorLog;

public enum VisitStatus
{
    Inside,      // ยังอยู่ในสำนักงาน
    CheckedOut,  // ออกแล้ว
    Cancelled    // ยกเลิกรายการ (แทนการลบ)
}

/// <summary>บันทึกการมาติดต่อหนึ่งครั้ง</summary>
public sealed class Visit
{
    public int Id { get; internal set; }
    public string VisitorName { get; init; } = "";   // ชื่อผู้มาติดต่อ
    public string Organization { get; init; } = "";  // หน่วยงานต้นสังกัด
    public string HostName { get; init; } = "";      // ผู้ที่ต้องการพบ
    public string Purpose { get; init; } = "";       // วัตถุประสงค์
    public DateTime CheckIn { get; init; }           // วันเวลาเข้า
    public DateTime? CheckOut { get; internal set; } // วันเวลาออก (null = ยังไม่ออก)

    public bool IsCancelled { get; internal set; }
    public string? CancelReason { get; internal set; }
    public DateTime? CancelledAt { get; internal set; }

    public VisitStatus Status =>
        IsCancelled ? VisitStatus.Cancelled :
        CheckOut.HasValue ? VisitStatus.CheckedOut :
        VisitStatus.Inside;

    internal Visit Clone() => (Visit)MemberwiseClone();
}
