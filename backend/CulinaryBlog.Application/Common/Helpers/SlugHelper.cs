using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Common.Helpers;

public static class SlugHelper
{
    public static string Generate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category =
                CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var slug = builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd');

        slug = Regex.Replace(
            slug,
            @"[^a-z0-9\s-]",
            string.Empty);

        slug = Regex.Replace(
            slug,
            @"[\s-]+",
            "-");

        return slug.Trim('-');
    }
}