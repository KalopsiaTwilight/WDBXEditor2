using System;
using System.Globalization;
using System.Windows.Controls;

namespace WDBXEditor2.Controls
{
    public class NumericValidationRule : ValidationRule
    {
        private readonly string nonNumericErrorMessage = "Only numeric input allowed.";
        private readonly string malformedInputErrorMessage = "Input is malformed.";
        private readonly string decimalSeperatorInputErrorMessage = "Only a single decimal seperator allowed.";
        private TextBox Source { get; }

        public NumericValidationRule(TextBox source) => this.Source = source;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            ArgumentNullException.ThrowIfNull(cultureInfo, nameof(cultureInfo));

            if (value is not string textValue
              || string.IsNullOrWhiteSpace(textValue))
            {
                return new ValidationResult(false, this.nonNumericErrorMessage);
            }

            if (IsInputNumeric(textValue, cultureInfo))
            {
                return ValidationResult.ValidResult;
            }

            // Input was can still be a valid special character
            // like '-', '+' or the decimal seperator of the current culture
            ValidationResult validationResult = HandleSpecialNonNumericCharacter(textValue, cultureInfo);

            return validationResult;
        }

        private bool IsInputNumeric(string input, IFormatProvider culture) =>
          double.TryParse(input, NumberStyles.Number, culture, out _);

        private ValidationResult HandleSpecialNonNumericCharacter(string input, CultureInfo culture)
        {
            ValidationResult validationResult;

            switch (input)
            {
                // Negative sign is not the first character
                case var _ when input.LastIndexOf(culture.NumberFormat.NegativeSign, StringComparison.OrdinalIgnoreCase) != 0:
                    validationResult = new ValidationResult(false, this.malformedInputErrorMessage);
                    break;

                // Positivre sign is not the first character
                case var _ when input.LastIndexOf(culture.NumberFormat.PositiveSign, StringComparison.OrdinalIgnoreCase) != 0:
                    validationResult = new ValidationResult(false, this.malformedInputErrorMessage);
                    break;

                // Allow single decimal separator
                case var _ when input.Equals(culture.NumberFormat.NumberDecimalSeparator, StringComparison.OrdinalIgnoreCase):
                    {
                        bool isSingleSeperator = !this.Source.Text.Contains(culture.NumberFormat.NumberDecimalSeparator, StringComparison.CurrentCultureIgnoreCase);
                        validationResult = isSingleSeperator ? ValidationResult.ValidResult : new ValidationResult(false, this.decimalSeperatorInputErrorMessage);
                        break;
                    }
                default:
                    validationResult = new ValidationResult(false, this.nonNumericErrorMessage);
                    break;
            }

            return validationResult;
        }
    }
}
