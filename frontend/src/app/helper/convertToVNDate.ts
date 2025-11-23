export function convertToVietnameseDate(datetimeStr: string): string {
  if (!datetimeStr || typeof datetimeStr !== 'string') return 'N/A';

  const normalized = datetimeStr.trim().replace(' ', 'T');
  const hasTZ = /([+-]\d{2}:\d{2}|Z)$/.test(normalized);
  const isDateOnly = /^\d{4}-\d{2}-\d{2}$/.test(normalized);

  try {
    let date: Date;

    if (hasTZ) {
      // Chuỗi có timezone (ví dụ: 2025-10-14T08:00:00Z)
      date = new Date(normalized);
    } else if (isDateOnly) {
      // Chỉ có ngày (không giờ)
      date = new Date(normalized + 'T00:00:00Z');
    } else {
      date = new Date(normalized + 'Z');
    }

    if (isNaN(date.getTime())) return 'N/A';

    return new Intl.DateTimeFormat('vi-VN', {
      timeZone: 'Asia/Ho_Chi_Minh', // ép hiển thị giờ VN
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    }).format(date);
  } catch {
    return 'N/A';
  }
}
