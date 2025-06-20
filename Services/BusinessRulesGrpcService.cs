
using Grpc.Core;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace SecureFileExchange.Services;

public class BusinessRulesGrpcService : Contracts.BusinessRulesService.BusinessRulesServiceBase
{
    private readonly ILogger<BusinessRulesGrpcService> _logger;
    private readonly VendorSettings _vendorSettings;

    public BusinessRulesGrpcService(
        ILogger<BusinessRulesGrpcService> logger,
        IOptions<VendorSettings> vendorSettings)
    {
        _logger = logger;
        _vendorSettings = vendorSettings.Value;
    }

    public override async Task<ValidationResult> ValidateRecords(ValidateRecordsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Validating records for vendor {VendorId}", request.VendorId);

        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == request.VendorId);
        if (vendor == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Vendor {request.VendorId} not found"));
        }

        var result = new ValidationResult
        {
            IsValid = true,
            CorrelationId = request.CorrelationId
        };

        var discrepancies = new List<Discrepancy>();

        foreach (var record in request.Records)
        {
            var recordDiscrepancies = ValidateRecord(record, vendor);
            discrepancies.AddRange(recordDiscrepancies);
        }

        result.Discrepancies.AddRange(discrepancies);
        result.IsValid = !discrepancies.Any();

        _logger.LogInformation("Validation completed for vendor {VendorId}. Valid: {IsValid}, Discrepancies: {Count}", 
            request.VendorId, result.IsValid, discrepancies.Count);

        return result;
    }

    private List<Discrepancy> ValidateRecord(FileRecord record, VendorConfiguration vendor)
    {
        var discrepancies = new List<Discrepancy>();

        foreach (var rule in vendor.ValidationRules)
        {
            if (!record.Fields.TryGetValue(rule.FieldName, out var fieldValue))
            {
                if (rule.IsRequired)
                {
                    discrepancies.Add(new Discrepancy
                    {
                        RecordId = record.RecordId,
                        FieldName = rule.FieldName,
                        ExpectedValue = "Required field",
                        ActualValue = "Missing",
                        RuleType = rule.RuleType,
                        Description = $"Required field {rule.FieldName} is missing"
                    });
                }
                continue;
            }

            var validationResult = ValidateField(fieldValue, rule);
            if (!validationResult.IsValid)
            {
                discrepancies.Add(new Discrepancy
                {
                    RecordId = record.RecordId,
                    FieldName = rule.FieldName,
                    ExpectedValue = rule.ExpectedValue ?? rule.Pattern ?? "Valid value",
                    ActualValue = fieldValue,
                    RuleType = rule.RuleType,
                    Description = validationResult.ErrorMessage
                });
            }
        }

        return discrepancies;
    }

    private (bool IsValid, string ErrorMessage) ValidateField(string value, ValidationRule rule)
    {
        switch (rule.RuleType.ToLowerInvariant())
        {
            case "regex":
                if (!string.IsNullOrEmpty(rule.Pattern))
                {
                    var regex = new Regex(rule.Pattern);
                    if (!regex.IsMatch(value))
                    {
                        return (false, $"Value '{value}' does not match pattern '{rule.Pattern}'");
                    }
                }
                break;

            case "range":
                if (decimal.TryParse(value, out var numericValue))
                {
                    if (rule.MinValue.HasValue && numericValue < rule.MinValue.Value)
                    {
                        return (false, $"Value {numericValue} is less than minimum {rule.MinValue.Value}");
                    }
                    if (rule.MaxValue.HasValue && numericValue > rule.MaxValue.Value)
                    {
                        return (false, $"Value {numericValue} is greater than maximum {rule.MaxValue.Value}");
                    }
                }
                else
                {
                    return (false, $"Value '{value}' is not a valid number");
                }
                break;

            case "length":
                if (rule.MinLength.HasValue && value.Length < rule.MinLength.Value)
                {
                    return (false, $"Value length {value.Length} is less than minimum {rule.MinLength.Value}");
                }
                if (rule.MaxLength.HasValue && value.Length > rule.MaxLength.Value)
                {
                    return (false, $"Value length {value.Length} is greater than maximum {rule.MaxLength.Value}");
                }
                break;

            case "exactvalue":
                if (!string.IsNullOrEmpty(rule.ExpectedValue) && value != rule.ExpectedValue)
                {
                    return (false, $"Expected '{rule.ExpectedValue}' but got '{value}'");
                }
                break;

            case "date":
                if (!DateTime.TryParse(value, out _))
                {
                    return (false, $"Value '{value}' is not a valid date");
                }
                break;

            default:
                return (false, $"Unknown validation rule type: {rule.RuleType}");
        }

        return (true, string.Empty);
    }
}
