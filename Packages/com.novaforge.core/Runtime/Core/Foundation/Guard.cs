using System;
using System.Text.RegularExpressions;

namespace NovaForge.Core.Foundation
{
    internal static class Guard
    {
        // Identifiers used as save keys, currency ids, product ids etc. Lowercase, digits, '_', '.', '-'.
        private static readonly Regex IdPattern = new Regex("^[a-z0-9][a-z0-9_.\\-]{0,63}$", RegexOptions.CultureInvariant);

        public static string Id(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value) || !IdPattern.IsMatch(value))
                throw new ArgumentException($"'{value}' is not a valid identifier (expected ^[a-z0-9][a-z0-9_.-]{{0,63}}$).", paramName);
            return value;
        }

        public static bool IsValidId(string value) => !string.IsNullOrEmpty(value) && IdPattern.IsMatch(value);

        public static T NotNull<T>(T value, string paramName) where T : class
            => value ?? throw new ArgumentNullException(paramName);
    }
}
