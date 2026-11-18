using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VisitorLog.Pages;

public class IndexModel : PageModel
{
    private static readonly string[] LocalFormats = { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" };
    private static readonly CultureInfo Thai = CreateThai();

    private readonly VisitorLogService _service;
    private readonly IVisitRepository _repository;

    public IndexModel(VisitorLogService service, IVisitRepository repository)
    {
        _service = service;
        _repository = repository;
    }

    public record RegisterForm(string? Name, string? Org, string? Host, string? Purpose, string? CheckIn);

    // ---------- ข้อมูลที่ใช้แสดงผล ----------
    public string Tab { get; private set; } = "inside";
    public IReadOnlyList<Visit> Rows { get; private set; } = Array.Empty<Visit>();
    public int InsideCount { get; private set; }
    public int OutCount { get; private set; }
    public int CancelledCount { get; private set; }
    public int AllCount { get; private set; }

    public RegisterForm Form { get; private set; } = new(null, null, null, null, null);
    public string? RegisterError { get; private set; }

    // แถวที่เกิดข้อผิดพลาด จะกางฟอร์มของแถวนั้นค้างไว้
    public int? OpenId { get; private set; }
    public string? OpenKind { get; private set; }
    public string? RowError { get; private set; }
    private string? _echo;

    public (string Key, string Label, int Count)[] Tabs => new (string Key, string Label, int Count)[]
    {
        ("inside", "ยังอยู่", InsideCount),
        ("out", "ออกแล้ว", OutCount),
        ("cancelled", "ยกเลิก", CancelledCount),
        ("all", "ทั้งหมด", AllCount)
    };

    public string EmptyText => Tab switch
    {
        "inside" => "ตอนนี้ไม่มีผู้มาติดต่ออยู่ในสำนักงาน",
        "out" => "ยังไม่มีผู้ที่ออกจากสำนักงาน",
        "cancelled" => "ไม่มีรายการที่ถูกยกเลิก",
        _ => "ยังไม่มีรายการ ลงทะเบียนผู้มาติดต่อรายแรกได้ที่ฟอร์มด้านข้าง"
    };

    public string StorageNote => _repository is PostgresVisitRepository
        ? "ข้อมูลบันทึกในฐานข้อมูล Supabase"
        : "โหมดทดลอง: ข้อมูลตัวอย่างเป็นบุคคลสมมติ เก็บในหน่วยความจำ ปิดโปรแกรมแล้วข้อมูลจะหาย";

    public string CheckInValue => Form.CheckIn ?? IsoLocal(DateTime.Now);

    // ---------- ตัวช่วยแสดงผลในหน้า ----------
    public static string IsoLocal(DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    public static string Fmt(DateTime? value) =>
        value.HasValue ? value.Value.ToString("d MMM HH:mm", Thai) : "—";

    /// <summary>ค่าเริ่มต้นช่องเวลาออก: เวลาปัจจุบัน แต่ไม่ให้น้อยกว่าเวลาเข้า</summary>
    public string OutValue(Visit v)
    {
        if (OpenId == v.Id && OpenKind == "out" && _echo is not null) return _echo;
        var now = DateTime.Now;
        return IsoLocal(now < v.CheckIn ? v.CheckIn : now);
    }

    public string ReasonValue(Visit v) =>
        OpenId == v.Id && OpenKind == "cancel" ? _echo ?? "" : "";

    public bool IsOpen(Visit v, string kind) => OpenId == v.Id && OpenKind == kind;

    // ---------- การทำงานของหน้า ----------
    public async Task OnGetAsync(string? tab)
    {
        Tab = NormalizeTab(tab);
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostRegisterAsync(string? name, string? org, string? host,
                                                         string? purpose, string? checkIn)
    {
        Form = new RegisterForm(name, org, host, purpose, checkIn);

        if (!TryParseLocal(checkIn, out var when))
        {
            RegisterError = "กรุณาระบุวันเวลาเข้า";
            await LoadAsync();
            return Page();
        }

        try
        {
            await _service.CheckInAsync(name ?? "", org ?? "", host ?? "", purpose ?? "", when);
        }
        catch (ArgumentException ex)
        {
            RegisterError = ex.Message;
            await LoadAsync();
            return Page();
        }

        return RedirectToPage(new { tab = "inside" });
    }

    public async Task<IActionResult> OnPostCheckOutAsync(int id, string? outTime, string? tab)
    {
        Tab = NormalizeTab(tab);

        if (!TryParseLocal(outTime, out var when))
            return await FailAsync(id, "out", "กรุณาระบุวันเวลาออก", outTime);

        try
        {
            await _service.CheckOutAsync(id, when);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return await FailAsync(id, "out", ex.Message, outTime);
        }

        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string? reason, string? tab)
    {
        Tab = NormalizeTab(tab);

        try
        {
            await _service.CancelAsync(id, reason ?? "");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return await FailAsync(id, "cancel", ex.Message, reason);
        }

        return RedirectToPage(new { tab = Tab });
    }

    // ---------- ภายใน ----------
    private async Task<IActionResult> FailAsync(int id, string kind, string message, string? echo)
    {
        OpenId = id;
        OpenKind = kind;
        RowError = message;
        _echo = echo;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var all = await _service.GetAllAsync();
        AllCount = all.Count;
        InsideCount = all.Count(v => v.Status == VisitStatus.Inside);
        OutCount = all.Count(v => v.Status == VisitStatus.CheckedOut);
        CancelledCount = all.Count(v => v.Status == VisitStatus.Cancelled);

        Rows = Tab switch
        {
            "inside" => all.Where(v => v.Status == VisitStatus.Inside).ToList(),
            "out" => all.Where(v => v.Status == VisitStatus.CheckedOut).ToList(),
            "cancelled" => all.Where(v => v.Status == VisitStatus.Cancelled).ToList(),
            _ => all
        };
    }

    private static string NormalizeTab(string? tab) =>
        tab is "inside" or "out" or "cancelled" or "all" ? tab : "inside";

    // ใช้รูปแบบสากลอ่านค่าจากช่อง datetime-local เสมอ ไม่ให้ปี พ.ศ. ของเครื่องทำให้ปีเพี้ยน
    private static bool TryParseLocal(string? text, out DateTime value) =>
        DateTime.TryParseExact(text, LocalFormats, CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out value);

    private static CultureInfo CreateThai()
    {
        try { return new CultureInfo("th-TH"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}
