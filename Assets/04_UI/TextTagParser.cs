using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

public static class TextTagParser
{
    private static readonly Regex TagRegex =
        new Regex(@"\{([^:]+):([^}]+)\}");

    public static string Parse(string originalText)
    {
        if (string.IsNullOrEmpty(originalText))
            return originalText;

        return TagRegex.Replace(originalText, match =>
        {
            string category = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();

            switch (category)
            {
                case "Action":
                    return ParseAction(value);

                default:
                    return match.Value;
            }
        });
    }

    private static string ParseAction(string value)
    {
        string actionName = value;
        string partName = string.Empty;

        // Move/Up 형태 처리
        if (value.Contains("/"))
        {
            string[] split = value.Split('/');

            actionName = split[0];

            if (split.Length > 1)
                partName = split[1];
        }

        var action = InputSystem.actions?.FindAction(actionName);

        if (action == null)
            return $"[{actionName}]";

        // Move/Up, Move/Left 같은 Composite 파트
        if (!string.IsNullOrEmpty(partName))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];

                if (binding.name.Equals(
                    partName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return action.GetBindingDisplayString(i);
                }
            }

            return $"[{actionName}/{partName}]";
        }

        // Keyboard 바인딩 우선
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];

            if (string.IsNullOrEmpty(binding.path))
                continue;

            if (binding.path.Contains("<Keyboard>"))
            {
                return action.GetBindingDisplayString(i);
            }
        }

        // Keyboard 없으면 첫 번째 표시 가능한 바인딩
        for (int i = 0; i < action.bindings.Count; i++)
        {
            string display = action.GetBindingDisplayString(i);

            if (!string.IsNullOrEmpty(display))
                return display;
        }

        return $"[{actionName}]";
    }
}