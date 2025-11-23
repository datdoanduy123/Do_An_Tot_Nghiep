namespace DocTask.Service.Helpers;

public static class FrequencyConverter
{
  public static int ToId(string frequency)
  {
    if (string.IsNullOrWhiteSpace(frequency))
      return 0;

    return frequency.Trim().ToLower() switch
    {
      "daily" => 1,
      "weekly" => 2,
      "monthly" => 3,
      _ => 0
    };
  }
  public static string ToName(int frequencyId)
  {
    return frequencyId switch
    {
      1 => "daily",
      2 => "weekly",
      3 => "monthly",
      _ => "unknown"
    };
  }
}
