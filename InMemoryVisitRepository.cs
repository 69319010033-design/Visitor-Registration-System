namespace VisitorLog;

/// <summary>เก็บในหน่วยความจำ ใช้ทดลองและใช้ในชุดทดสอบ ปิดโปรแกรมแล้วข้อมูลหาย</summary>
public sealed class InMemoryVisitRepository : IVisitRepository
{
    private readonly object _sync = new();
    private readonly List<Visit> _items = new();
    private int _nextId = 1;

    public Task<Visit> AddAsync(Visit visit, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var stored = visit.Clone();
            stored.Id = _nextId++;
            _items.Add(stored);
            return Task.FromResult(stored.Clone());
        }
    }

    public Task<Visit?> FindAsync(int id, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var found = _items.FirstOrDefault(v => v.Id == id);
            return Task.FromResult<Visit?>(found?.Clone());
        }
    }

    public Task<IReadOnlyList<Visit>> ListAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            IReadOnlyList<Visit> list = _items
                .OrderBy(v => v.CheckIn).ThenBy(v => v.Id)
                .Select(v => v.Clone())
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task SaveAsync(Visit visit, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var index = _items.FindIndex(v => v.Id == visit.Id);
            if (index < 0) throw new KeyNotFoundException($"ไม่พบรายการ #{visit.Id}");
            _items[index] = visit.Clone();
            return Task.CompletedTask;
        }
    }
}
