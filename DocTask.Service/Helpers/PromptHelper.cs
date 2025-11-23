using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocTask.Service.Helpers
{
    public static class PromptHelper
    {
        public static string Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            else
            {
                return Regex.Replace(input.Trim(), @"^\s+", "", RegexOptions.Multiline);
            }
        }
    }
}