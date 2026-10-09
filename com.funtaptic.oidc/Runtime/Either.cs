using System;

namespace Funtaptic.OIDC
{
    /// <summary>Contains exactly one of two possible values.</summary>
    public sealed class Either<TLeft, TRight>
    {
        private readonly TLeft _left;
        private readonly TRight _right;

        public bool IsLeft { get; }
        public bool IsRight => !IsLeft;
        public TLeft Left => IsLeft ? _left : throw new InvalidOperationException("This Either contains a right value.");
        public TRight Right => IsRight ? _right : throw new InvalidOperationException("This Either contains a left value.");

        private Either(bool isLeft, TLeft left, TRight right)
        {
            IsLeft = isLeft;
            _left = left;
            _right = right;
        }

        public static implicit operator Either<TLeft, TRight>(TLeft value) => FromLeft(value);
        public static implicit operator Either<TLeft, TRight>(TRight value) => FromRight(value);

        public static Either<TLeft, TRight> FromLeft(TLeft value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            return new Either<TLeft, TRight>(true, value, default);
        }

        public static Either<TLeft, TRight> FromRight(TRight value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            return new Either<TLeft, TRight>(false, default, value);
        }
    }
}
