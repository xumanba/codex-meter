using System;
using System.Globalization;
using System.Text;

namespace CodexMeter
{
    internal static class RolloutJsonFields
    {
        internal static string ExtractString(string line, string key, int startIndex)
        {
            if (String.IsNullOrEmpty(line) || String.IsNullOrEmpty(key))
                return null;

            int keyIndex = line.IndexOf(key, Math.Max(0, startIndex), StringComparison.Ordinal);
            if (keyIndex < 0)
                return null;
            int colon = line.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return null;
            int quote = line.IndexOf('"', colon + 1);
            if (quote < 0)
                return null;

            StringBuilder value = new StringBuilder();
            bool escaped = false;
            for (int index = quote + 1; index < line.Length; index++)
            {
                char character = line[index];
                if (escaped)
                {
                    if (character == 'n')
                        value.Append('\n');
                    else if (character == 'r')
                        value.Append('\r');
                    else if (character == 't')
                        value.Append('\t');
                    else
                        value.Append(character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    return value.ToString();
                }
                else
                {
                    value.Append(character);
                }
            }
            return null;
        }

        internal static bool TryExtractLong(
            string line, string key, int startIndex, out long value)
        {
            value = 0;
            string number;
            if (!TryExtractNumber(line, key, startIndex, out number))
                return false;
            return Int64.TryParse(number, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        internal static bool TryExtractDouble(
            string line, string key, int startIndex, out double value)
        {
            value = 0;
            string number;
            if (!TryExtractNumber(line, key, startIndex, out number))
                return false;
            return Double.TryParse(number, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractNumber(
            string line, string key, int startIndex, out string number)
        {
            number = null;
            if (String.IsNullOrEmpty(line) || String.IsNullOrEmpty(key))
                return false;

            int keyIndex = line.IndexOf(key, Math.Max(0, startIndex), StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;
            int colon = line.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return false;

            int index = colon + 1;
            while (index < line.Length && Char.IsWhiteSpace(line[index]))
                index++;
            int end = index;
            while (end < line.Length && (Char.IsDigit(line[end]) || line[end] == '-' ||
                line[end] == '+' || line[end] == '.' || line[end] == 'e' || line[end] == 'E'))
            {
                end++;
            }
            if (end <= index)
                return false;
            number = line.Substring(index, end - index);
            return true;
        }
    }
}
