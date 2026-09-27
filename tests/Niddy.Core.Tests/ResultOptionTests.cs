using Niddy.Results;

namespace Niddy.Core.Tests;

public class ResultTests
{
    private static Result<int> Parse(string text) =>
        int.TryParse(text, out var value) ? value : new Error($"'{text}' is not a number", "parse");

    [Fact]
    public void ImplicitConversions_CreateSuccessAndFailure()
    {
        var ok = Parse("42");
        var bad = Parse("x");

        Assert.True(ok.IsSuccess);
        Assert.Equal(42, ok.Value);
        Assert.Null(ok.Error);
        Assert.True(bad.IsFailure);
        Assert.Equal("parse", bad.Error!.Code);
        Assert.Throws<ResultException>(() => bad.Value);
    }

    [Fact]
    public void Default_IsAFailure()
    {
        Result<int> result = default;

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Uninitialized, result.Error);
    }

    [Fact]
    public void MapBindEnsure_ChainAndShortCircuit()
    {
        var doubled = Parse("21").Map(x => x * 2).Ensure(x => x > 40, "too small");
        var failed = Parse("x").Map(x => x * 2);
        var rejected = Parse("1").Ensure(x => x > 40, "too small");
        var bound = Parse("5").Bind(x => x > 0 ? Result.Success(x.ToString()) : Result<string>.Failure("negative"));

        Assert.Equal(42, doubled.Value);
        Assert.Equal("parse", failed.Error!.Code);
        Assert.Equal("too small", rejected.Error!.Message);
        Assert.Equal("5", bound.Value);
    }

    [Fact]
    public void Match_PicksTheBranch()
    {
        Assert.Equal("ok 1", Parse("1").Match(v => $"ok {v}", e => e.Message));
        Assert.StartsWith("'x'", Parse("x").Match(v => $"ok {v}", e => e.Message));
    }

    [Fact]
    public void Try_CatchesExceptionsAsErrors()
    {
        var result = Result.Try<int>(() => throw new FormatException("bad"));

        Assert.True(result.IsFailure);
        Assert.IsType<FormatException>(result.Error!.Exception);
        Assert.Equal("bad", result.Error.Message);
    }

    [Fact]
    public async Task TryAsync_DoesNotSwallowCancellation()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Result.TryAsync<int>(() => throw new OperationCanceledException()));
    }

    [Fact]
    public void Combine_ReturnsTheFirstFailure()
    {
        var combined = Result.Combine(Result.Success(), Result.Failure("first"), Result.Failure("second"));

        Assert.Equal("first", combined.Error!.Message);
        Assert.True(Result.Combine(Result.Success(), Result.Success()).IsSuccess);
    }

    [Fact]
    public void Deconstruct_And_TryGetValue()
    {
        var (isSuccess, value, error) = Parse("7");
        Assert.True(isSuccess);
        Assert.Equal(7, value);
        Assert.Null(error);

        Assert.False(Parse("x").TryGetValue(out _));
        Assert.Equal(-1, Parse("x").GetValueOrDefault(-1));
    }

    [Fact]
    public void Equality_ComparesValuesAndErrors()
    {
        Assert.Equal(Parse("3"), Parse("3"));
        Assert.NotEqual(Parse("3"), Parse("4"));
        Assert.Equal(Parse("x"), Parse("x"));
    }

    [Fact]
    public void ToOption_DropsTheError()
    {
        Assert.Equal(Option.Some(3), Parse("3").ToOption());
        Assert.True(Parse("x").ToOption().IsNone);
    }
}

public class OptionTests
{
    [Fact]
    public void NullConvertsToNone()
    {
        string? missing = null;
        Option<string> none = missing;
        Option<string> some = "hi";

        Assert.True(none.IsNone);
        Assert.Equal("hi", some.Value);
        Assert.Throws<InvalidOperationException>(() => none.Value);
    }

    [Fact]
    public void Default_IsNone() => Assert.True(default(Option<int>).IsNone);

    [Fact]
    public void Some_RejectsNull() => Assert.Throws<ArgumentNullException>(() => Option.Some<string>(null!));

    [Fact]
    public void MapWhereOr_Compose()
    {
        var some = Option.Some(5);

        Assert.Equal(Option.Some(10), some.Map(x => x * 2));
        Assert.True(some.Where(x => x > 10).IsNone);
        Assert.Equal(Option.Some(1), Option<int>.None.Or(() => Option.Some(1)));
        Assert.True(Option.Some("a").Map(_ => (string?)null).IsNone);
    }

    [Fact]
    public void ToResult_UsesTheErrorForNone()
    {
        Assert.Equal("missing", Option<int>.None.ToResult("missing").Error!.Message);
        Assert.Equal(4, Option.Some(4).ToResult("missing").Value);
    }

    [Fact]
    public void Extensions_FindValues()
    {
        var dictionary = new Dictionary<string, int> { ["a"] = 1 };

        Assert.Equal(Option.Some(1), dictionary.GetOption("a"));
        Assert.True(dictionary.GetOption("b").IsNone);
        Assert.Equal(Option.Some(2), new[] { 1, 2, 3 }.FirstOrNone(x => x % 2 == 0));
        Assert.True(Array.Empty<int>().FirstOrNone().IsNone);
    }

    [Fact]
    public void ToString_ShowsTheValue()
    {
        Assert.Equal("Some(3)", Option.Some(3).ToString());
        Assert.Equal("None", Option<int>.None.ToString());
    }
}
