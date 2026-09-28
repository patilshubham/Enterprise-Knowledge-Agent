using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Errors;

/// <summary>TASK-ERR-01 (ASDD §22): category → safe Problem Details shape mapping.</summary>
public sealed class ErrorCategoryHttpMappingTests
{
    [Theory]
    [InlineData(ErrorCategory.Validation, 400)]
    [InlineData(ErrorCategory.Authentication, 401)]
    [InlineData(ErrorCategory.Authorization, 403)]
    [InlineData(ErrorCategory.Conflict, 409)]
    [InlineData(ErrorCategory.Configuration, 500)]
    [InlineData(ErrorCategory.DependencyUnavailable, 503)]
    [InlineData(ErrorCategory.Throttled, 429)]
    [InlineData(ErrorCategory.Timeout, 504)]
    [InlineData(ErrorCategory.ContextOverflow, 400)]
    [InlineData(ErrorCategory.RetrievalEmpty, 422)]
    [InlineData(ErrorCategory.EmbeddingFailure, 502)]
    [InlineData(ErrorCategory.GenerationFailure, 502)]
    [InlineData(ErrorCategory.StorageFailure, 503)]
    [InlineData(ErrorCategory.QueueFailure, 503)]
    [InlineData(ErrorCategory.OutputValidationFailure, 502)]
    [InlineData(ErrorCategory.InternalFailure, 500)]
    public void Resolve_maps_every_category_to_its_expected_status_code(ErrorCategory category, int expectedStatus)
    {
        ErrorCategoryHttpMapping.Resolve(category).StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public void Resolve_every_enum_value_has_a_mapping_with_non_empty_fields()
    {
        foreach (var category in Enum.GetValues<ErrorCategory>())
        {
            var mapping = ErrorCategoryHttpMapping.Resolve(category);

            mapping.Title.Should().NotBeNullOrWhiteSpace();
            mapping.SafeDetail.Should().NotBeNullOrWhiteSpace();
            mapping.RecommendedAction.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(ErrorCategory.DependencyUnavailable)]
    [InlineData(ErrorCategory.Throttled)]
    [InlineData(ErrorCategory.Timeout)]
    [InlineData(ErrorCategory.StorageFailure)]
    [InlineData(ErrorCategory.QueueFailure)]
    public void Resolve_retryable_categories_report_IsRetryable_true(ErrorCategory category)
    {
        ErrorCategoryHttpMapping.Resolve(category).IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void Resolve_non_retryable_category_reports_IsRetryable_false()
    {
        ErrorCategoryHttpMapping.Resolve(ErrorCategory.Validation).IsRetryable.Should().BeFalse();
    }
}
