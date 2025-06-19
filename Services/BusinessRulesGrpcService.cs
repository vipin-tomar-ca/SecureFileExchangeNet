
using Grpc.Core;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;

namespace SecureFileExchange.Services;

public class BusinessRulesGrpcService : BusinessRulesService.BusinessRulesServiceBase
{
    private readonly ILogger<BusinessRulesGrpcService> _logger;
    private readonly IConfiguration _configuration;

    public BusinessRulesGrpcService(ILogger<BusinessRulesGrpcService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override async Task<ValidationResult> ValidateRecords(ValidateRecordsRequest request, ServerCallContext context)
    {
        var result = new ValidationResult
        {
            IsValid = true,
            CorrelationId = request.CorrelationId
        };

        try
        {
            var vendorConfig = GetVendorConfig(request.VendorId);
            
            foreach (var record in request.Records)
            {
                await ValidateRecord(record, vendorConfig.Rules, result);
            }

            result.IsValid = result.Discrepancies.Count == 0;
            
            _logger.LogInformation("Validation completed for vendor {VendorId} with {DiscrepancyCount} discrepancies. Correlation ID: {CorrelationId}", 
                request.VendorId, result.Discrepancies.Count, request.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating records for vendor {VendorId}. Correlation ID: {CorrelationId}", 
                request.VendorId, request.CorrelationId);
            
            result.Discrepancies.Add(new Discrepancy
            {
                RecordId = "SYSTEM",
                Description = $"Validation error: {ex.Message}",
                RuleType = "SYSTEM_ERROR"
            });
            result.IsValid = false;
        }

        return result;
    }

    private async Task ValidateRecord(FileRecord record, ValidationRules rules, ValidationResult result)
    {
        // Validate field rules
        foreach (var fieldRule in rules.FieldRules)
        {
            if (!record.Fields.TryGetValue(fieldRule.FieldName, out var fieldValue))
            {
                if (fieldRule.Required)
                {
                    result.Discrepancies.Add(new Discrepancy
                    {
                        RecordId = record.RecordId,
                        FieldName = fieldRule.FieldName,
                        ExpectedValue = "Required field",
                        ActualValue = "Missing",
                        RuleType = "REQUIRED_FIELD",
                        Description = $"Required field {fieldRule.FieldName} is missing"
                    });
                }
                continue;
            }

            // Validate data type, length, pattern, etc.
            await ValidateFieldValue(record.RecordId, fieldRule, fieldValue, result);
        }

        // Validate business rules
        foreach (var businessRule in rules.BusinessRules)
        {
            await ValidateBusinessRule(record, businessRule, result);
        }
    }

    private async Task ValidateFieldValue(string recordId, FieldRule rule, string value, ValidationResult result)
    {
        // Length validation
        if (rule.MinLength.HasValue && value.Length < rule.MinLength.Value)
        {
            result.Discrepancies.Add(new Discrepancy
            {
                RecordId = recordId,
                FieldName = rule.FieldName,
                ExpectedValue = $"Minimum length {rule.MinLength.Value}",
                ActualValue = $"Length {value.Length}",
                RuleType = "MIN_LENGTH",
                Description = $"Field {rule.FieldName} is too short"
            });
        }

        if (rule.MaxLength.HasValue && value.Length > rule.MaxLength.Value)
        {
            result.Discrepancies.Add(new Discrepancy
            {
                RecordId = recordId,
                FieldName = rule.FieldName,
                ExpectedValue = $"Maximum length {rule.MaxLength.Value}",
                ActualValue = $"Length {value.Length}",
                RuleType = "MAX_LENGTH",
                Description = $"Field {rule.FieldName} is too long"
            });
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(rule.Pattern))
        {
            var regex = new System.Text.RegularExpressions.Regex(rule.Pattern);
            if (!regex.IsMatch(value))
            {
                result.Discrepancies.Add(new Discrepancy
                {
                    RecordId = recordId,
                    FieldName = rule.FieldName,
                    ExpectedValue = $"Pattern: {rule.Pattern}",
                    ActualValue = value,
                    RuleType = "PATTERN_MISMATCH",
                    Description = $"Field {rule.FieldName} does not match required pattern"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task ValidateBusinessRule(FileRecord record, BusinessRule rule, ValidationResult result)
    {
        // Simple expression evaluation - in production, consider using a proper expression engine
        try
        {
            // TODO: Implement business rule expression evaluation
            // For now, just log that the rule would be evaluated
            _logger.LogDebug("Evaluating business rule {RuleId} for record {RecordId}", rule.RuleId, record.RecordId);
        }
        catch (Exception ex)
        {
            result.Discrepancies.Add(new Discrepancy
            {
                RecordId = record.RecordId,
                RuleType = "BUSINESS_RULE_ERROR",
                Description = $"Error evaluating business rule {rule.RuleId}: {ex.Message}"
            });
        }

        await Task.CompletedTask;
    }

    private VendorSettings GetVendorConfig(string vendorId)
    {
        var vendorSection = _configuration.GetSection($"Vendors:{vendorId}");
        return vendorSection.Get<VendorSettings>() ?? throw new InvalidOperationException($"Vendor configuration not found for {vendorId}");
    }
}
