namespace VisitorLog;

/// <summary>ที่เก็บข้อมูลการมาติดต่อ (หน่วยความจำ หรือ Supabase/Postgres)</summary>
public interface IVisitRepository
{
    /// <summary>เพิ่มรายการใหม่ และคืนรายการที่ได้รหัสแล้ว</summary>
    Task<Visit> AddAsync(Visit visit, CancellationToken ct = default);

    Task<Visit?> FindAsync(int id, CancellationToken ct = default);

    /// <summary>ทุกรายการ เรียงตามวันเวลาเข้า แล้วตามรหัส</summary>
    Task<IReadOnlyList<Visit>> ListAsync(CancellationToken ct = default);

    /// <summary>บันทึกฟิลด์ที่เปลี่ยนได้ (เวลาออก และการยกเลิก)</summary>
    Task SaveAsync(Visit visit, CancellationToken ct = default);
}
