using UnityEngine;

namespace RTS.DataValidation
{
    public enum ValidationSeverity { Warning, Error }

    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Code { get; }
        public Object Asset { get; }
        public string FieldPath { get; }
        public string Message { get; }
        public Object RelatedAsset { get; }

        public ValidationIssue(ValidationSeverity severity, string code, Object asset,
            string fieldPath, string message, Object relatedAsset = null)
        {
            Severity = severity;
            Code = code;
            Asset = asset;
            FieldPath = fieldPath;
            Message = message;
            RelatedAsset = relatedAsset;
        }
    }
}
