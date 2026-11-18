namespace VisitorLog;

/// <summary>
/// ระบบลงทะเบียนผู้มาติดต่อ (ตรรกะและกติกาอยู่ที่นี่ ส่วนการเก็บข้อมูลอยู่ที่ IVisitRepository)
/// กฎสำคัญ:
///  - วันเวลาออกต้องไม่น้อยกว่าวันเวลาเข้า (เท่ากันได้)
///  - ไม่มีการลบรายการ ใช้การยกเลิกพร้อมเหตุผลเท่านั้น
/// </summary>
public sealed class VisitorLogService
{
    private readonly IVisitRepository _repo;
    private readonly Func<DateTime> _clock;

    public VisitorLogService(IVisitRepository repository, Func<DateTime>? clock = null)
    {
        _repo = repository;
        _clock = clock ?? (() => DateTime.Now);
    }

    /// <summary>ลงทะเบียนผู้มาติดต่อเข้าสำนักงาน</summary>
    public async Task<Visit> CheckInAsync(string visitorName, string organization, string hostName,
                                          string purpose, DateTime? checkIn = null,
                                          CancellationToken ct = default)
    {
        RequireText(visitorName, "ชื่อผู้มาติดต่อ");
        RequireText(organization, "หน่วยงานต้นสังกัด");
        RequireText(hostName, "ผู้ที่ต้องการพบ");
        RequireText(purpose, "วัตถุประสงค์");

        var visit = new Visit
        {
            VisitorName = visitorName.Trim(),
            Organization = organization.Trim(),
            HostName = hostName.Trim(),
            Purpose = purpose.Trim(),
            CheckIn = checkIn ?? _clock()
        };
        return await _repo.AddAsync(visit, ct);
    }

    /// <summary>บันทึกการออกจากสำนักงาน (วันเวลาออกต้อง ≥ วันเวลาเข้า)</summary>
    public async Task<Visit> CheckOutAsync(int id, DateTime? checkOut = null,
                                           CancellationToken ct = default)
    {
        var visit = await GetRequiredAsync(id, ct);

        if (visit.IsCancelled)
            throw new InvalidOperationException($"รายการ #{id} ถูกยกเลิกแล้ว ไม่สามารถบันทึกเวลาออกได้");
        if (visit.CheckOut.HasValue)
            throw new InvalidOperationException($"รายการ #{id} บันทึกเวลาออกไปแล้ว");

        var time = checkOut ?? _clock();
        if (time < visit.CheckIn)
            throw new ArgumentException(
                $"วันเวลาออก ({time:yyyy-MM-dd HH:mm:ss}) ต้องไม่น้อยกว่าวันเวลาเข้า ({visit.CheckIn:yyyy-MM-dd HH:mm:ss})");

        visit.CheckOut = time;
        await _repo.SaveAsync(visit, ct);
        return visit;
    }

    /// <summary>ยกเลิกรายการแทนการลบ ต้องระบุเหตุผล รายการยังคงอยู่ในระบบเพื่อตรวจสอบย้อนหลัง</summary>
    public async Task<Visit> CancelAsync(int id, string reason, DateTime? cancelledAt = null,
                                         CancellationToken ct = default)
    {
        RequireText(reason, "เหตุผลการยกเลิก");

        var visit = await GetRequiredAsync(id, ct);
        if (visit.IsCancelled)
            throw new InvalidOperationException($"รายการ #{id} ถูกยกเลิกไปแล้ว");

        visit.IsCancelled = true;
        visit.CancelReason = reason.Trim();
        visit.CancelledAt = cancelledAt ?? _clock();
        await _repo.SaveAsync(visit, ct);
        return visit;
    }

    public Task<Visit?> FindAsync(int id, CancellationToken ct = default) =>
        _repo.FindAsync(id, ct);

    public async Task<IReadOnlyList<Visit>> GetAllAsync(CancellationToken ct = default) =>
        (await _repo.ListAsync(ct)).OrderBy(v => v.CheckIn).ThenBy(v => v.Id).ToList();

    /// <summary>ผู้ที่ยังอยู่ในสำนักงาน (เข้าแล้ว ยังไม่ออก และไม่ถูกยกเลิก)</summary>
    public Task<IReadOnlyList<Visit>> GetInsideAsync(CancellationToken ct = default) =>
        ByStatusAsync(VisitStatus.Inside, ct);

    /// <summary>ผู้ที่ออกจากสำนักงานแล้ว</summary>
    public Task<IReadOnlyList<Visit>> GetCheckedOutAsync(CancellationToken ct = default) =>
        ByStatusAsync(VisitStatus.CheckedOut, ct);

    /// <summary>รายการที่ถูกยกเลิก</summary>
    public Task<IReadOnlyList<Visit>> GetCancelledAsync(CancellationToken ct = default) =>
        ByStatusAsync(VisitStatus.Cancelled, ct);

    private async Task<IReadOnlyList<Visit>> ByStatusAsync(VisitStatus status, CancellationToken ct) =>
        (await GetAllAsync(ct)).Where(v => v.Status == status).ToList();

    private async Task<Visit> GetRequiredAsync(int id, CancellationToken ct) =>
        await _repo.FindAsync(id, ct) ?? throw new KeyNotFoundException($"ไม่พบรายการ #{id}");

    private static void RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"กรุณาระบุ{fieldName}");
    }
}
