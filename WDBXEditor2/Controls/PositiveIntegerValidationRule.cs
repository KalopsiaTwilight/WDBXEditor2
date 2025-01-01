using System;
using System.Globalization;
using System.Windows.Controls;

namespace WDBXEditor2.Controls
{
    public class PositiveIntegerValidationRule : ValidationRule
    {
        private readonly string nonNumericErrorMessage = "Only positive numbers are allowed.";
        private TextBox Source { get; }

        public PositiveIntegerValidationRule(TextBox source) => this.Source = source;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            ArgumentNullException.ThrowIfNull(cultureInfo, nameof(cultureInfo));

            if (value is not string textValue
              || string.IsNullOrWhiteSpace(textValue))
            {
                return new ValidationResult(false, this.nonNumericErrorMessage);
            }

            if (IsInputValid(textValue, cultureInfo))
            {
                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, this.nonNumericErrorMessage);
        }

        private bool IsInputValid(string input, IFormatProvider culture) => uint.TryParse(input, NumberStyles.Number, culture, out _);
    }
}
