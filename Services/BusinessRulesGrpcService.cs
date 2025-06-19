using Grpc.Core;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using Microsoft.Extensions.Options;

namespace SecureFileExchange.Services;

public class BusinessRulesGrpcService : Contracts.BusinessRulesService.BusinessRulesServiceBase
{
    private readonly ILogger<BusinessRulesGrpcService> _logger;
    private readonly VendorSettings _vendorSettings;

    public BusinessRulesGrpcService(ILogger<BusinessRulesGrpcService> logger, IOptions<VendorSettings> vendorSettings)
    {
        _logger = logger;
        _vendorSettings = vendorSettings.Value;
    }

    public override Task<ValidationResult> ValidateRecords(ValidateRecordsRequest request, ServerCallContext context)
    {
        var result = new ValidationResult
        {
            IsValid = true,
            CorrelationId = request.CorrelationId
        };

        try
        {
            var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == request.VendorId);
            if (vendor == null)
            {
                result.IsValid = false;
                result.Discrepancies.Add(new Discrepancy
                {
                    RecordId = "VENDOR",
                    FieldName = "vendor_id",
                    Description = $"Unknown vendor: {request.VendorId}",
                    RuleType = "VENDOR_VALIDATION"
                });
                return Task.FromResult(result);
            }

            foreach (var record in request.Records)
            {
                foreach (var rule in vendor.ValidationRules)
                {
                    if (record.Fields.TryGetValue(rule.FieldName, out var fieldValue))
                    {
                        if (!ValidateField(fieldValue, rule))
                        {
                            result.IsValid = false;
                            result.Discrepancies.Add(new Discrepancy
                            {
                                RecordId = record.RecordId,
                                FieldName = rule.FieldName,
                                ActualValue = fieldValue,
                                ExpectedValue = rule.ExpectedValue,
                                RuleType = rule.RuleType,
                                Description = rule.Description
                            });
                        }
                    }
                }
            }

            _logger.LogInformation("Validation completed for {VendorId}. Valid: {IsValid}, Discrepancies: {Count}", 
                request.VendorId, result.IsValid, result.Discrepancies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating records for vendor {VendorId}", request.VendorId);
            result.IsValid = false;
        }

        return Task.FromResult(result);
    }

    private bool ValidateField(string value, ValidationRule rule)
    {
        return rule.RuleType switch
        {
            "REQUIRED" => !string.IsNullOrEmpty(value),
            "REGEX" => System.Text.RegularExpressions.Regex.IsMatch(value, rule.ExpectedValue),
            "LENGTH" => value.Length <= int.Parse(rule.ExpectedValue),
            "NUMERIC" => decimal.TryParse(value, out _),
            _ => true
        };
    }
}