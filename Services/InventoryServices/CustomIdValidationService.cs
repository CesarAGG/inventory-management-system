using InventoryManagementSystem.Models.CustomId;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using System.Text;

namespace InventoryManagementSystem.Services.InventoryServices
{
    public class CustomIdValidationService : ICustomIdValidationService
    {
        public bool IsIdValid(string id, List<IdSegment> formatSegments)
        {
            if (formatSegments == null || !formatSegments.Any())
            {
                return string.IsNullOrEmpty(id);
            }

            var regexPattern = new StringBuilder("^");
            foreach (var segment in formatSegments)
            {
                regexPattern.Append(GetRegexForSegment(segment));
            }
            regexPattern.Append('$');

            return Regex.IsMatch(id, regexPattern.ToString());
        }

        private string GetRegexForSegment(IdSegment segment)
        {
            return segment switch
            {
                FixedTextSegment s => Regex.Escape(s.Value),
                SequenceSegment s => $"\\d{{{s.Padding},}}",
                DateSegment s => ".+", // Simple match; true validation is complex.
                RandomNumbersSegment s => s.Format switch
                {
                    "20-bit" => "\\d{1,7}", // Max is 1,048,575
                    "32-bit" => "\\d{1,10}", // Max is 2,147,483,647
                    "6-digit" => "\\d{6}",
                    "9-digit" => "\\d{9}",
                    _ => ""
                },
                GuidSegment s => s.Format switch
                {
                    "N" => "[0-9a-fA-F]{32}",
                    "D" => "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
                    "B" => "\\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\}",
                    "P" => "\\([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\)",
                    _ => ""
                },
                _ => ""
            };
        }
    }
}