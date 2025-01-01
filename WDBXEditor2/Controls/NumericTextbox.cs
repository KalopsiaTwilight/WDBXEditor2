using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows;

namespace WDBXEditor2.Controls
{
    public abstract class BaseNumericTextbox : TextBox
    {
        private ValidationRule NumericInputValidationRule { get; set; }

        public BaseNumericTextbox(Func<TextBox, ValidationRule> ruleCtor)
        {
            NumericInputValidationRule = ruleCtor(this);
            DataObject.AddPastingHandler(this, OnContentPasting);
        }

        private void OnContentPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                ShowErrorFeedback("Only numeric content supported.");

                return;
            }

            string pastedtext = (string)e.DataObject.GetData(DataFormats.Text);
            CultureInfo culture = CultureInfo.CurrentCulture;
            ValidationResult validationResult = ValidateText(pastedtext, culture);
            if (!validationResult.IsValid)
            {
                e.CancelCommand();
            }
        }

        #region Overrides of TextBoxBase

        /// <inheritdoc />
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            string currentTextInput = e.Text;

            // Remove any negative sign if '+' was pressed
            // or prepend a negative sign if '-' was pressed
            if (TryHandleNumericSign(currentTextInput, culture))
            {
                e.Handled = true;
                return;
            }

            ValidationResult validationResult = ValidateText(currentTextInput, culture);
            e.Handled = !validationResult.IsValid;
            if (validationResult.IsValid)
            {
                base.OnTextInput(e);
            }
        }

        #endregion Overrides of TextBoxBase

        private ValidationResult ValidateText(string currentTextInput, CultureInfo culture)
        {
            ValidationResult validationResult = this.NumericInputValidationRule.Validate(currentTextInput, culture);

            if (validationResult.IsValid)
            {
                HideErrorFeedback();
            }
            else
            {
                ShowErrorFeedback(validationResult.ErrorContent);
            }

            return validationResult;
        }

        private bool TryHandleNumericSign(string input, CultureInfo culture)
        {
            int oldCaretPosition = this.CaretIndex;

            // Remove any negative sign if '+' pressed
            if (input.Equals(culture.NumberFormat.PositiveSign, StringComparison.OrdinalIgnoreCase))
            {
                if (this.Text.StartsWith(culture.NumberFormat.NegativeSign, StringComparison.OrdinalIgnoreCase))
                {
                    this.Text = this.Text.Remove(0, 1);

                    // Move the caret to the original input position
                    this.CaretIndex = oldCaretPosition - 1;
                }

                return true;
            }
            // Prepend the negative sign if '-' pressed
            else if (input.Equals(culture.NumberFormat.NegativeSign, StringComparison.OrdinalIgnoreCase))
            {
                if (!this.Text.StartsWith(culture.NumberFormat.NegativeSign, StringComparison.OrdinalIgnoreCase))
                {
                    this.Text = this.Text.Insert(0, culture.NumberFormat.NegativeSign);

                    // Move the caret to the original input position
                    this.CaretIndex = oldCaretPosition + 1;
                }

                return true;
            }

            return false;
        }

        private void HideErrorFeedback()
        {
            BindingExpression textPropertyBindingExpression = GetBindingExpression(TextProperty);
            bool hasTextPropertyBinding = textPropertyBindingExpression is not null;
            if (hasTextPropertyBinding)
            {
                Validation.ClearInvalid(textPropertyBindingExpression);
            }
        }

        private void ShowErrorFeedback(object errorContent)
        {
            BindingExpression textPropertyBindingExpression = GetBindingExpression(TextProperty);
            bool hasTextPropertyBinding = textPropertyBindingExpression is not null;
            if (hasTextPropertyBinding)
            {
                // Show the error feedbck by triggering the binding engine
                // to show the Validation.ErrorTemplate
                Validation.MarkInvalid(
                  textPropertyBindingExpression,
                  new ValidationError(
                    this.NumericInputValidationRule,
                    textPropertyBindingExpression,
                    errorContent, // The error message
                    null));
            }
        }
    }

    public class NumericTextBox : BaseNumericTextbox
    {
        public NumericTextBox() : base((tb) => new NumericValidationRule(tb))
        {
        }
    }

    public class PositiveIntegerTextBox : BaseNumericTextbox
    {
        public PositiveIntegerTextBox() : base((tb) => new PositiveIntegerValidationRule(tb))
        {
        }
    }
}
