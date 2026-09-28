using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Errors;

/// <summary>TASK-JOB-06 (ASDD §12.4): the retryable/terminal truth table shared by both workers.</summary>
public sealed class RetryClassifierTests
{
    [Theory]
    [InlineData(ErrorCategory.DependencyUnavailable)]
    [InlineData(ErrorCategory.Throttled)]
    [InlineData(ErrorCategory.Timeout)]
    [InlineData(ErrorCategory.StorageFailure)]
    [InlineData(ErrorCategory.QueueFailure)]
    public void Classify_retryable_categories_returns_retryable(ErrorCategory category)
    {
        RetryClassifier.Classify(category).Should().Be(RetryDisposition.Retryable);
        RetryClassifier.IsRetryable(category).Should().BeTrue();
    }

    [Theory]
    [InlineData(ErrorCategory.Validation)]
    [InlineData(ErrorCategory.Authentication)]
    [InlineData(ErrorCategory.Authorization)]
    [InlineData(ErrorCategory.Conflict)]
    [InlineData(ErrorCategory.Configuration)]
    [InlineData(ErrorCategory.ContextOverflow)]
    [InlineData(ErrorCategory.RetrievalEmpty)]
    [InlineData(ErrorCategory.EmbeddingFailure)]
    [InlineData(ErrorCategory.GenerationFailure)]
    [InlineData(ErrorCategory.OutputValidationFailure)]
    [InlineData(ErrorCategory.InternalFailure)]
    public void Classify_terminal_categories_returns_terminal(ErrorCategory category)
    {
        RetryClassifier.Classify(category).Should().Be(RetryDisposition.Terminal);
        RetryClassifier.IsRetryable(category).Should().BeFalse();
    }

    [Fact]
    public void ShouldAutoRetry_retryable_category_under_max_attempts_returns_true()
    {
        RetryClassifier.ShouldAutoRetry(ErrorCategory.Timeout, attemptNumber: 1, maxAttempts: 5).Should().BeTrue();
    }

    [Fact]
    public void ShouldAutoRetry_retryable_category_at_max_attempts_returns_false()
    {
        RetryClassifier.ShouldAutoRetry(ErrorCategory.Timeout, attemptNumber: 5, maxAttempts: 5).Should().BeFalse();
    }

    [Fact]
    public void ShouldAutoRetry_terminal_category_never_retries_regardless_of_attempt_count()
    {
        RetryClassifier.ShouldAutoRetry(ErrorCategory.Validation, attemptNumber: 0, maxAttempts: 5).Should().BeFalse();
    }

    [Fact]
    public void ShouldAutoRetry_uses_default_max_attempts_when_not_specified()
    {
        RetryClassifier.ShouldAutoRetry(ErrorCategory.Timeout, attemptNumber: RetryClassifier.DefaultMaxAttempts - 1)
            .Should().BeTrue();
        RetryClassifier.ShouldAutoRetry(ErrorCategory.Timeout, attemptNumber: RetryClassifier.DefaultMaxAttempts)
            .Should().BeFalse();
    }

    [Fact]
    public void All_error_categories_are_classified_without_throwing()
    {
        foreach (var category in Enum.GetValues<ErrorCategory>())
        {
            var act = () => RetryClassifier.Classify(category);
            act.Should().NotThrow();
        }
    }
}
