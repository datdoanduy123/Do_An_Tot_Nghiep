namespace DocTask.Service.Helpers;

public static class UserConverter
{
  public static int ToId(string frequency)
  {
    if (string.IsNullOrWhiteSpace(frequency))
      return 0;

    return frequency.Trim().ToLower() switch
    {
      "Tống Khánh Huy" or "huytong" => 6, 
      "Đỗ Đức Nam" or "namdo" => 7,
      "Phạm Đức Hải" or "haipham" => 8,
      "Nguyễn Đức Anh" or "anhnguyen" => 10,
      "Trần Tuấn Kiệt" or "kiettran" => 11,
      "Nguyễn Xuân Mạnh" or "manhnguyen" => 12,
      "Lưu Quốc Khải" or "khailuu" => 14,
      "Phạm Minh Tuấn" or "tuanpham" => 15,
      "Đoàn Duy Đạt" or "datdoan" => 18,
      "Nguyễn Văn An" or "annguyen" => 20,
      "Mai Phương Thúy" or "thuymai" => 21,
      "Ngô Phúc Trường" or "truongngo" => 22, 
      _ => 0
    };
  }
  public static string ToName(int frequencyId)
  {
    return frequencyId switch
    {
      6 => "huytong",
      7 => "namdo",
      8 => "haipham",
      10 => "anhnguyen",
      11 => "kiettran",
      12 => "manhnguyen",
      14 => "khailuu",
      15 => "tuanpham",
      18 => "datndoan",
      20 => "annguuyen",
      21 => "thuymai",
      22 => "truongngo",
      _ => "unknown"
    };
  }
}
