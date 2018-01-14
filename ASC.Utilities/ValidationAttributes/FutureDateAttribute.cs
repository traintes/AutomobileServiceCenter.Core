using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ASC.Utilities.ValidationAttributes
{
    public class FutureDateAttribute : ValidationAttribute, IClientModelValidator
    {
        private readonly int _days;
        private readonly string _errorMessage = "Date cannot be after {0} days from current date.";

        public FutureDateAttribute() { }
        public FutureDateAttribute(int days)
        {
            this._days = days;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            DateTime date = (DateTime)value;
            if (date > DateTime.UtcNow.AddDays(_days))
                return new ValidationResult(string.Format(this._errorMessage, this._days));
            return ValidationResult.Success;
        }

        public void AddValidation(ClientModelValidationContext context)
        {
            context.Attributes.Add("data-val-futuredate", string.Format(this._errorMessage, this._days));
            context.Attributes.Add("data-val-futuredate-days", this._days.ToString());
        }
    }
}
