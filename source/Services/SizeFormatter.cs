using System.Globalization;

namespace PlayniteStorageView.Services
{
    /// <summary>
    /// Binary-unit byte formatter:
    ///   ≥ 1 GiB → one decimal place (e.g. "71.2 GiB")
    ///   ≥ 1 MiB → no decimal      (e.g. "820 MiB")
    ///   otherwise                  (e.g. "12 KiB")
    /// </summary>
    public static class SizeFormatter
    {
        private const ulong KiB = 1024UL;
        private const ulong MiB = 1024UL * 1024UL;
        private const ulong GiB = 1024UL * 1024UL * 1024UL;

        public static string Format(ulong bytes)
        {
            if (bytes >= GiB)
            {
                double v = bytes / (double)GiB;
                return v.ToString("0.0", CultureInfo.CurrentCulture) + " GiB";
            }
            if (bytes >= MiB)
            {
                double v = bytes / (double)MiB;
                return v.ToString("0", CultureInfo.CurrentCulture) + " MiB";
            }
            if (bytes >= KiB)
            {
                double v = bytes / (double)KiB;
                return v.ToString("0", CultureInfo.CurrentCulture) + " KiB";
            }
            return bytes.ToString(CultureInfo.CurrentCulture) + " B";
        }
    }
}
