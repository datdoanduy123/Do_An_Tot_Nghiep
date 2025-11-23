import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import timezone from 'dayjs/plugin/timezone';

dayjs.extend(utc);
dayjs.extend(timezone);

export function convertDateTimeToISO(input: {
  date: string;
  time: string;
}): string {
  const [day, month, year] = input.date.split('/').map(Number);
  const [hour, minute] = input.time.split(':').map(Number);

  // Tạo chuỗi thời gian theo định dạng YYYY-MM-DDTHH:mm
  const formatted = `${year}-${String(month).padStart(2, '0')}-${String(
    day
  ).padStart(2, '0')}T${String(hour).padStart(2, '0')}:${String(
    minute
  ).padStart(2, '0')}`;

  // Parse ở múi giờ Việt Nam và convert sang UTC
  return dayjs.tz(formatted, 'Asia/Ho_Chi_Minh').utc().toISOString();
}

export function convertDateArrayToISOStrings(input: string): string {
  // Dùng Date parse để lấy timestamp chính xác
  const date = new Date(input); // JS Date hiểu được định dạng này
  const dt = dayjs(date).tz('Asia/Ho_Chi_Minh');

  return dt.toISOString();
}
