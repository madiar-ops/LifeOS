namespace LifeOS.Application.Common;

/// <summary>
/// Результат операции без возвращаемого значения.
/// Используется там, где неуспех — это ожидаемый бизнес-сценарий (например,
/// неверный пароль), а не исключительная ситуация. Исключения остаются
/// для действительно исключительных случаев.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Успешный результат не может содержать ошибку.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Неуспешный результат обязан содержать ошибку.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>Результат операции с возвращаемым значением.</summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    /// <summary>Доступно только при IsSuccess == true.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Нельзя читать Value у неуспешного результата.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
