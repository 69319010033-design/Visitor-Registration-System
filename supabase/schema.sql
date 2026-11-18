-- ทะเบียนผู้มาติดต่อสำนักงาน: ตารางสำหรับ Supabase
-- วิธีใช้: เปิด Supabase > SQL Editor > วางไฟล์นี้ทั้งหมด > Run

create table if not exists public.visits (
  id             integer generated always as identity primary key,
  visitor_name   text      not null,   -- ชื่อผู้มาติดต่อ
  organization   text      not null,   -- หน่วยงานต้นสังกัด
  host_name      text      not null,   -- ผู้ที่ต้องการพบ
  purpose        text      not null,   -- วัตถุประสงค์
  check_in       timestamp not null,   -- วันเวลาเข้า (เวลาบนนาฬิกา ไม่มีโซนเวลา)
  check_out      timestamp,            -- วันเวลาออก (null = ยังอยู่ในสำนักงาน)
  is_cancelled   boolean   not null default false,
  cancel_reason  text,                 -- เหตุผลการยกเลิก (ใช้แทนการลบ)
  cancelled_at   timestamp,
  created_at     timestamptz not null default now(),

  constraint visits_visitor_name_not_blank check (btrim(visitor_name) <> ''),
  constraint visits_organization_not_blank check (btrim(organization) <> ''),
  constraint visits_host_name_not_blank    check (btrim(host_name) <> ''),
  constraint visits_purpose_not_blank      check (btrim(purpose) <> ''),

  -- วันเวลาออกต้องไม่น้อยกว่าวันเวลาเข้า (เท่ากันได้)
  constraint visits_checkout_not_before_checkin
    check (check_out is null or check_out >= check_in),

  -- ยกเลิกต้องมีเหตุผล
  constraint visits_cancel_requires_reason
    check (not is_cancelled or btrim(coalesce(cancel_reason, '')) <> '')
);

create index if not exists visits_check_in_idx on public.visits (check_in);

-- ความปลอดภัย: ไม่ให้เรียกตารางนี้ผ่าน API สาธารณะของ Supabase (anon / authenticated)
-- แอป C# เชื่อมต่อด้วยบทบาท postgres โดยตรง จึงไม่ถูกจำกัดด้วยข้อนี้
alter table public.visits enable row level security;
revoke all on public.visits from anon, authenticated;
