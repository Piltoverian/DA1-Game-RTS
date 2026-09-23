using System.Collections.Generic;

namespace RTS.DataValidation
{
    /// <summary>Read-only validation of registered definitions and their permitted references. No UnityEditor dependency.</summary>
    public static class GameDataValidator
    {
        public static IReadOnlyList<ValidationIssue> Validate(GameDataRegistry registry)
        {
            var context = new ValidationContext();
            if (context.Required(registry, registry, "Registry")) context.Visit(registry);
            return context.Issues.AsReadOnly();
        }
    }
}
