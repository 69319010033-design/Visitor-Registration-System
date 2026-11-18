-- ข้อมูลบุคคลสมมติสำหรับทดลอง (ไม่จำเป็นต้องรัน)
-- เวลาอิงจากวันนี้ตามเวลาประเทศไทย

with today as (
  select (now() at time zone 'Asia/Bangkok')::date as d
)
insert into public.visits (visitor_name, organization, host_name, purpose, check_in, check_out)
select v.*
from today,
lateral (values
  ('สมชาย ใจดี',     'บริษัท ตัวอย่างวิศวกรรม จำกัด', 'คุณอรทัย (ฝ่ายบุคคล)',  'นัดหมายเรื่องสัญญาจ้าง', today.d + time '08:30', today.d + time '10:00'),
  ('สุดา รักเรียน',   'สำนักงานเขตสมมติ',            'คุณวิชัย (ธุรการ)',     'ส่งหนังสือราชการ',      today.d + time '09:15', today.d + time '09:15'),
  ('ประเสริฐ ทองดี', 'บริษัท เอบีซี ซัพพลาย จำกัด',   'คุณนภา (ฝ่ายจัดซื้อ)',  'เสนอราคาสินค้า',        today.d + time '10:20', null::timestamp),
  ('มาลี สายสุข',     'มหาวิทยาลัยสมมติ',            'ผอ.กิตติ (ฝ่ายวิชาการ)', 'ประชุมความร่วมมือ',     today.d + time '10:45', null::timestamp)
) as v(visitor_name, organization, host_name, purpose, check_in, check_out);

-- รายการที่ยกเลิกพร้อมเหตุผล
insert into public.visits (visitor_name, organization, host_name, purpose, check_in,
                           is_cancelled, cancel_reason, cancelled_at)
select 'วีระ มั่นคง', 'บริษัท ทดสอบ จำกัด', 'คุณอรทัย (ฝ่ายบุคคล)', 'สัมภาษณ์งาน',
       (now() at time zone 'Asia/Bangkok')::date + time '11:00',
       true, 'เจ้าหน้าที่กรอกชื่อผิดคน',
       (now() at time zone 'Asia/Bangkok')::date + time '11:02';
